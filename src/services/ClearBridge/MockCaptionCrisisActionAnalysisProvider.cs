using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class MockCaptionCrisisActionAnalysisProvider : ICrisisActionAnalysisProvider
    {
        public const string SampleTranscript =
            "[1] Today we are reviewing the community service worksheet.\n" +
            "[2] Please submit the worksheet by Friday through Google Classroom.\n" +
            "[3] The reading example about last year's project is only an example, not a new assignment.\n" +
            "[4] I will post clarification if the due date changes.";

        public string Name => "Mock";

        public Task<CrisisActionAnalysisResult> AnalyzeAsync(
            string sourceText,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = outputLanguage switch
            {
                ClearBridgeOutputLanguages.SimplifiedChinese => BuildChineseResult(sourceText),
                ClearBridgeOutputLanguages.Arabic => BuildArabicResult(sourceText),
                _ => BuildEnglishResult(sourceText)
            };
            return Task.FromResult(result);
        }

        private static CrisisActionAnalysisResult BuildEnglishResult(string sourceText)
        {
            var signals = DetectSignals(sourceText);
            var result = BuildBaseResult(
                signals,
                "Caption analysis",
                "The selected captions were reviewed in Mock Mode. Only information present in the selected captions is included.",
                "The selected captions do not clearly state a required action.",
                "The selected captions contain uncertain wording that should be reviewed before acting.",
                "A worksheet or submission was mentioned.",
                "A Friday deadline was mentioned.",
                "Google Classroom was mentioned as the submission location.",
                "The selected captions indicate a worksheet submission.",
                "Submit the worksheet.",
                "Friday",
                "community service worksheet",
                "The captions do not provide an exact time on Friday.",
                "The captions do not clearly state the submission location.",
                "The captions contain uncertain wording; review the original captions before acting.",
                "Do not treat an example as a new assignment unless the speaker explicitly says it is required.",
                "The worksheet or submission is supported by the selected captions.",
                "The deadline is supported by the selected captions.",
                "The submission location is supported by the selected captions.",
                "The example warning is supported by the selected captions.",
                "The unclear item is supported by the selected captions.");

            if (signals.HasAction)
            {
                result.Title = signals.HasDeadline || signals.HasLocation
                    ? "Worksheet submission from selected captions"
                    : "Caption action item";
                result.Summary = "The selected captions mention a worksheet or submission. Review the action item and any unclear details before saving.";
            }
            else if (signals.HasAmbiguity)
            {
                result.Title = "Unclear caption instructions";
                result.Summary = "The selected captions contain uncertain statements. They should be reviewed before treating them as confirmed instructions.";
            }
            else
            {
                result.Title = "No required action found";
                result.Summary = "The selected captions do not clearly state a required task, deadline, or submission location.";
            }

            return result;
        }

        private static CrisisActionAnalysisResult BuildChineseResult(string sourceText)
        {
            var signals = DetectSignals(sourceText);
            var result = BuildBaseResult(
                signals,
                "字幕分析",
                "已在 Mock Mode 中检查所选字幕。结果只包含所选字幕中出现的信息。",
                "所选字幕没有明确说明必须执行的行动。",
                "所选字幕包含不确定表述，行动前需要复核。",
                "字幕提到了 worksheet 或提交事项。",
                "字幕提到了周五截止。",
                "字幕提到 Google Classroom 是提交位置。",
                "所选字幕说明存在 worksheet 提交事项。",
                "提交 worksheet。",
                "周五",
                "community service worksheet",
                "字幕没有说明周五的具体截止时间。",
                "字幕没有明确说明提交位置。",
                "字幕包含不确定表述；行动前请复核原始字幕。",
                "除非发言人明确说明，否则不要把示例当成新作业。",
                "worksheet 或提交事项有字幕依据。",
                "截止日期有字幕依据。",
                "提交位置有字幕依据。",
                "示例警告有字幕依据。",
                "不确定项有字幕依据。");

            if (signals.HasAction)
            {
                result.Title = signals.HasDeadline || signals.HasLocation
                    ? "所选字幕中的 worksheet 提交事项"
                    : "字幕行动项";
                result.Summary = "所选字幕提到了 worksheet 或提交事项。保存前请复核行动项和不明确内容。";
            }
            else if (signals.HasAmbiguity)
            {
                result.Title = "不明确的字幕要求";
                result.Summary = "所选字幕包含不确定表述，不能直接当成已确认要求。";
            }
            else
            {
                result.Title = "未发现明确行动要求";
                result.Summary = "所选字幕没有明确说明必须完成的任务、截止日期或提交位置。";
            }

            return result;
        }

        private static CrisisActionAnalysisResult BuildArabicResult(string sourceText)
        {
            var signals = DetectSignals(sourceText);
            var result = BuildBaseResult(
                signals,
                "تحليل التسميات",
                "تمت مراجعة التسميات المحددة في Mock Mode. لا يتضمن الناتج إلا المعلومات الموجودة في التسميات المحددة.",
                "لا تذكر التسميات المحددة إجراء مطلوبا بوضوح.",
                "تتضمن التسميات المحددة عبارات غير مؤكدة ويجب مراجعتها قبل التصرف.",
                "ذكرت التسميات ورقة عمل أو عملية تسليم.",
                "ذكرت التسميات موعدا نهائيا يوم الجمعة.",
                "ذكرت التسميات Google Classroom كمكان للتسليم.",
                "تشير التسميات المحددة إلى تسليم ورقة عمل.",
                "إرسال ورقة العمل.",
                "الجمعة",
                "community service worksheet",
                "لا تذكر التسميات وقتا محددا يوم الجمعة.",
                "لا تذكر التسميات مكان التسليم بوضوح.",
                "تتضمن التسميات عبارات غير مؤكدة؛ راجع النص الأصلي قبل التصرف.",
                "لا تعامل المثال كواجب جديد ما لم يذكر المتحدث ذلك بوضوح.",
                "ورقة العمل أو التسليم مدعوم بالتسميات المحددة.",
                "الموعد النهائي مدعوم بالتسميات المحددة.",
                "مكان التسليم مدعوم بالتسميات المحددة.",
                "تحذير المثال مدعوم بالتسميات المحددة.",
                "البند غير الواضح مدعوم بالتسميات المحددة.");

            if (signals.HasAction)
            {
                result.Title = signals.HasDeadline || signals.HasLocation
                    ? "تسليم ورقة عمل من التسميات المحددة"
                    : "إجراء من التسميات";
                result.Summary = "تذكر التسميات المحددة ورقة عمل أو عملية تسليم. راجع الإجراء وأي تفاصيل غير واضحة قبل الحفظ.";
            }
            else if (signals.HasAmbiguity)
            {
                result.Title = "تعليمات غير واضحة في التسميات";
                result.Summary = "تتضمن التسميات المحددة عبارات غير مؤكدة، ولا يجب التعامل معها كتعليمات مؤكدة دون مراجعة.";
            }
            else
            {
                result.Title = "لم يتم العثور على إجراء مطلوب";
                result.Summary = "لا تذكر التسميات المحددة مهمة مطلوبة أو موعدا نهائيا أو مكان تسليم بوضوح.";
            }

            return result;
        }

        private static CrisisActionAnalysisResult BuildBaseResult(
            CaptionSignals signals,
            string defaultTitle,
            string defaultSummary,
            string noActionPoint,
            string ambiguityPoint,
            string worksheetPoint,
            string deadlinePoint,
            string locationPoint,
            string actionPoint,
            string actionTask,
            string localizedFriday,
            string worksheetDocument,
            string missingTimeUnclear,
            string missingLocationUnclear,
            string ambiguityUnclear,
            string exampleWarning,
            string actionClaim,
            string deadlineClaim,
            string locationClaim,
            string warningClaim,
            string unclearClaim)
        {
            return new CrisisActionAnalysisResult
            {
                Title = defaultTitle,
                Summary = defaultSummary,
                Priority = signals.HasAction ? "medium" : "low",
                ImportantPoints = BuildImportantPoints(signals, noActionPoint, ambiguityPoint, worksheetPoint, deadlinePoint, locationPoint),
                Actions = BuildActions(signals, actionTask, localizedFriday, worksheetDocument),
                UnclearItems = BuildUnclearItems(signals, missingTimeUnclear, missingLocationUnclear, ambiguityUnclear),
                Warnings = BuildWarnings(signals, exampleWarning),
                SourceEvidence = BuildEvidence(signals, actionClaim, deadlineClaim, locationClaim, warningClaim, unclearClaim)
            };
        }

        private static List<string> BuildImportantPoints(
            CaptionSignals signals,
            string noActionPoint,
            string ambiguityPoint,
            string worksheetPoint,
            string deadlinePoint,
            string locationPoint)
        {
            var points = new List<string>();
            if (signals.HasWorksheetOrSubmission)
                points.Add(worksheetPoint);
            if (signals.HasDeadline)
                points.Add(deadlinePoint);
            if (signals.HasLocation)
                points.Add(locationPoint);
            if (signals.HasAmbiguity)
                points.Add(ambiguityPoint);
            if (points.Count == 0)
                points.Add(noActionPoint);

            return points;
        }

        private static List<ActionItem> BuildActions(
            CaptionSignals signals,
            string actionTask,
            string localizedFriday,
            string worksheetDocument)
        {
            if (!signals.HasAction)
                return [];

            return
            [
                new ActionItem
                {
                    Task = actionTask,
                    Deadline = signals.HasDeadline ? localizedFriday : string.Empty,
                    Location = signals.HasLocation ? "Google Classroom" : string.Empty,
                    RequiredDocuments = signals.HasWorksheetOrSubmission ? [worksheetDocument] : []
                }
            ];
        }

        private static List<string> BuildUnclearItems(
            CaptionSignals signals,
            string missingTimeUnclear,
            string missingLocationUnclear,
            string ambiguityUnclear)
        {
            var unclearItems = new List<string>();
            if (signals.HasAction && signals.HasDeadline)
                unclearItems.Add(missingTimeUnclear);
            if (signals.HasAction && !signals.HasLocation)
                unclearItems.Add(missingLocationUnclear);
            if (signals.HasAmbiguity)
                unclearItems.Add(ambiguityUnclear);

            return unclearItems;
        }

        private static List<string> BuildWarnings(CaptionSignals signals, string exampleWarning)
        {
            return signals.HasExampleWarning ? [exampleWarning] : [];
        }

        private static List<SourceEvidenceItem> BuildEvidence(
            CaptionSignals signals,
            string actionClaim,
            string deadlineClaim,
            string locationClaim,
            string warningClaim,
            string unclearClaim)
        {
            var evidence = new List<SourceEvidenceItem>();
            if (signals.HasAction && !string.IsNullOrWhiteSpace(signals.ActionEvidence))
            {
                evidence.Add(new SourceEvidenceItem
                {
                    Claim = actionClaim,
                    SourceText = signals.ActionEvidence
                });
            }

            if (signals.HasDeadline && !string.IsNullOrWhiteSpace(signals.DeadlineEvidence))
            {
                evidence.Add(new SourceEvidenceItem
                {
                    Claim = deadlineClaim,
                    SourceText = signals.DeadlineEvidence
                });
            }

            if (signals.HasLocation && !string.IsNullOrWhiteSpace(signals.LocationEvidence))
            {
                evidence.Add(new SourceEvidenceItem
                {
                    Claim = locationClaim,
                    SourceText = signals.LocationEvidence
                });
            }

            if (signals.HasExampleWarning && !string.IsNullOrWhiteSpace(signals.WarningEvidence))
            {
                evidence.Add(new SourceEvidenceItem
                {
                    Claim = warningClaim,
                    SourceText = signals.WarningEvidence
                });
            }

            if (signals.HasAmbiguity && !string.IsNullOrWhiteSpace(signals.AmbiguityEvidence))
            {
                evidence.Add(new SourceEvidenceItem
                {
                    Claim = unclearClaim,
                    SourceText = signals.AmbiguityEvidence
                });
            }

            if (evidence.Count == 0 && !string.IsNullOrWhiteSpace(signals.FirstEvidence))
            {
                evidence.Add(new SourceEvidenceItem
                {
                    Claim = "Selected caption content",
                    SourceText = signals.FirstEvidence
                });
            }

            return evidence;
        }

        private static CaptionSignals DetectSignals(string sourceText)
        {
            var lines = ExtractLines(sourceText);
            var actionEvidence = FindLine(lines, line => ContainsAny(line, "submit", "turn in", "hand in"));
            var worksheetEvidence = FindLine(lines, line => ContainsAny(line, "worksheet"));
            var deadlineEvidence = FindLine(lines, line => ContainsAny(line, "friday"));
            var locationEvidence = FindLine(lines, line => ContainsAny(line, "google classroom"));
            var warningEvidence = FindLine(lines, line =>
                ContainsAll(line, "example", "not") ||
                ContainsAll(line, "only", "example"));
            var ambiguityEvidence = FindLine(lines, line =>
                ContainsAny(line, "maybe", "could change", "i think", "not sure", "ask someone later", "unclear"));

            var hasWorksheetOrSubmission = !string.IsNullOrWhiteSpace(worksheetEvidence) ||
                !string.IsNullOrWhiteSpace(actionEvidence);
            var hasAction = !string.IsNullOrWhiteSpace(actionEvidence) ||
                (!string.IsNullOrWhiteSpace(worksheetEvidence) &&
                 (!string.IsNullOrWhiteSpace(deadlineEvidence) || !string.IsNullOrWhiteSpace(locationEvidence)));

            return new CaptionSignals
            {
                HasWorksheetOrSubmission = hasWorksheetOrSubmission,
                HasAction = hasAction,
                HasDeadline = !string.IsNullOrWhiteSpace(deadlineEvidence),
                HasLocation = !string.IsNullOrWhiteSpace(locationEvidence),
                HasExampleWarning = !string.IsNullOrWhiteSpace(warningEvidence),
                HasAmbiguity = !string.IsNullOrWhiteSpace(ambiguityEvidence),
                ActionEvidence = string.IsNullOrWhiteSpace(actionEvidence) ? worksheetEvidence : actionEvidence,
                DeadlineEvidence = deadlineEvidence,
                LocationEvidence = locationEvidence,
                WarningEvidence = warningEvidence,
                AmbiguityEvidence = ambiguityEvidence,
                FirstEvidence = lines.FirstOrDefault() ?? string.Empty
            };
        }

        private static List<string> ExtractLines(string sourceText)
        {
            return sourceText
                .Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(StripCaptionNumber)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        private static string StripCaptionNumber(string line)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("[", StringComparison.Ordinal))
                return trimmed;

            var closingBracket = trimmed.IndexOf(']');
            if (closingBracket < 0 || closingBracket + 1 >= trimmed.Length)
                return trimmed;

            return trimmed[(closingBracket + 1)..].Trim();
        }

        private static string FindLine(IEnumerable<string> lines, Func<string, bool> predicate)
        {
            return lines.FirstOrDefault(predicate) ?? string.Empty;
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsAll(string text, params string[] values)
        {
            return values.All(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        private sealed class CaptionSignals
        {
            public bool HasWorksheetOrSubmission { get; init; }
            public bool HasAction { get; init; }
            public bool HasDeadline { get; init; }
            public bool HasLocation { get; init; }
            public bool HasExampleWarning { get; init; }
            public bool HasAmbiguity { get; init; }
            public string ActionEvidence { get; init; } = string.Empty;
            public string DeadlineEvidence { get; init; } = string.Empty;
            public string LocationEvidence { get; init; } = string.Empty;
            public string WarningEvidence { get; init; } = string.Empty;
            public string AmbiguityEvidence { get; init; } = string.Empty;
            public string FirstEvidence { get; init; } = string.Empty;
        }
    }
}

using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class MockRollingSummaryProvider : IRollingSummaryProvider
    {
        public string Name => "Mock Rolling Summary";

        public Task<RollingSummaryResult> AnalyzeBatchAsync(
            RollingSummaryRequest request,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = Math.Max(1, request.BatchNumber);
            var topic = Localize(
                outputLanguage,
                "School project planning",
                "学校项目安排",
                "تخطيط مشروع مدرسي");
            var summary = batch switch
            {
                1 => Localize(
                    outputLanguage,
                    "The speaker introduced the project and explained that details are still being collected.",
                    "发言人介绍了项目，并说明部分细节仍在收集中。",
                    "قدّم المتحدث المشروع وأوضح أن بعض التفاصيل ما زالت قيد الجمع."),
                2 => Localize(
                    outputLanguage,
                    "A worksheet task was added with a Friday deadline and Google Classroom as the submission location.",
                    "新增了 worksheet 任务，截止时间为周五，提交位置为 Google Classroom。",
                    "تمت إضافة مهمة ورقة عمل بموعد نهائي يوم الجمعة ومكان التسليم هو Google Classroom."),
                _ => Localize(
                    outputLanguage,
                    "The location was corrected and students were warned to monitor updates.",
                    "地点已被更正，并提醒学生继续关注更新。",
                    "تم تصحيح المكان وتم تنبيه الطلاب إلى متابعة التحديثات.")
            };

            var context = request.PreviousContext.Clone();
            context.CurrentTopic = topic;
            Merge(context.EstablishedFacts, summary);
            if (batch >= 2)
            {
                Merge(context.ConfirmedActions, Localize(outputLanguage, "Submit the worksheet.", "提交 worksheet。", "تسليم ورقة العمل."));
                Merge(context.DatesAndDeadlines, Localize(outputLanguage, "Friday at 5 PM.", "周五下午 5 点。", "الجمعة الساعة 5 مساء."));
                Merge(context.Locations, "Google Classroom");
            }
            if (batch >= 3)
            {
                context.Locations.RemoveAll(item => item.Contains("Room 204", StringComparison.OrdinalIgnoreCase));
                Merge(context.Locations, "Room 310");
                Merge(context.Warnings, Localize(outputLanguage, "The room changed from Room 204 to Room 310.", "地点从 Room 204 改为 Room 310。", "تغيرت القاعة من Room 204 إلى Room 310."));
                context.UnresolvedQuestions.RemoveAll(item => item.Contains("location", StringComparison.OrdinalIgnoreCase));
            }

            context.BatchCount = batch;
            context.LastProcessedSentenceNumber = request.RangeEnd;
            context.CompressedNarrative = string.Join(" ", context.EstablishedFacts.TakeLast(5));
            RollingSummaryJsonParser.ClampContext(context);

            var evidence = request.ProcessedSentences.FirstOrDefault()?.SourceText ?? string.Empty;
            var result = new RollingSummaryResult
            {
                CurrentTopic = topic,
                BatchSummary = summary,
                KeyPoints = [summary],
                NewActions = batch >= 2
                    ?
                    [
                        new ActionItem
                        {
                            Task = Localize(outputLanguage, "Submit the worksheet.", "提交 worksheet。", "تسليم ورقة العمل."),
                            Deadline = batch >= 2 ? Localize(outputLanguage, "Friday at 5 PM", "周五下午 5 点", "الجمعة الساعة 5 مساء") : string.Empty,
                            Location = "Google Classroom"
                        }
                    ]
                    : [],
                DatesAndDeadlines = batch >= 2 ? [Localize(outputLanguage, "Friday at 5 PM", "周五下午 5 点", "الجمعة الساعة 5 مساء")] : [],
                Locations = batch >= 3 ? ["Room 310"] : batch >= 2 ? ["Google Classroom"] : [],
                Warnings = batch >= 3 ? [Localize(outputLanguage, "Room 204 was superseded by Room 310.", "Room 204 已被 Room 310 替代。", "تم استبدال Room 204 بـ Room 310.")] : [],
                UnresolvedQuestions = batch == 1 ? [Localize(outputLanguage, "The final submission details are not yet clear.", "最终提交细节尚不清楚。", "تفاصيل التسليم النهائية غير واضحة بعد.")] : [],
                SourceEvidence = string.IsNullOrWhiteSpace(evidence)
                    ? []
                    :
                    [
                        new SourceEvidenceItem
                        {
                            Claim = summary,
                            SourceText = evidence
                        }
                    ],
                ContextCache = context
            };

            return Task.FromResult(result);
        }

        private static void Merge(List<string> values, string item)
        {
            if (!values.Any(value => string.Equals(value, item, StringComparison.OrdinalIgnoreCase)))
                values.Add(item);
        }

        private static string Localize(string outputLanguage, string english, string chinese, string arabic)
        {
            return outputLanguage switch
            {
                "Simplified Chinese" => chinese,
                "Arabic" => arabic,
                _ => english
            };
        }
    }
}

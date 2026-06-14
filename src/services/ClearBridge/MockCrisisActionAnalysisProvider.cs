using LiveCaptionsTranslator.models.ClearBridge;

namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class MockCrisisActionAnalysisProvider : ICrisisActionAnalysisProvider
    {
        public const string SampleNotice =
            "Due to extreme weather conditions, all outdoor extracurricular activities scheduled after 12:30 PM today are suspended. Students should check with their activity coordinators for indoor alternatives. Bus schedules have not been confirmed to change. Parents should monitor the school portal for further updates.";

        public string Name => "Mock";

        public Task<CrisisActionAnalysisResult> AnalyzeAsync(
            string sourceText,
            string outputLanguage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isChinese = outputLanguage == ClearBridgeOutputLanguages.SimplifiedChinese;
            var result = isChinese ? BuildChineseResult() : BuildEnglishResult();
            return Task.FromResult(result);
        }

        private static CrisisActionAnalysisResult BuildEnglishResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "Outdoor activities suspended after 12:30 PM",
                Summary = "Because of extreme weather, outdoor extracurricular activities after 12:30 PM today are suspended. Students need to ask activity coordinators about indoor alternatives, and parents should watch the school portal for updates.",
                Priority = "high",
                ImportantPoints =
                [
                    "Outdoor extracurricular activities after 12:30 PM today are suspended.",
                    "Students may have indoor alternatives, but they must confirm them with activity coordinators.",
                    "Bus schedule changes are not confirmed yet.",
                    "Parents should monitor the school portal for updates."
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "Ask the activity coordinator whether there is an indoor alternative.",
                        Deadline = "Today, before the scheduled activity after 12:30 PM",
                        Location = "School or activity coordinator contact channel",
                        RequiredDocuments = []
                    },
                    new ActionItem
                    {
                        Task = "Check the school portal for further updates.",
                        Deadline = "Today and until the school posts a new update",
                        Location = "School portal",
                        RequiredDocuments = []
                    }
                ],
                UnclearItems =
                [
                    "The notice does not confirm whether bus schedules will change.",
                    "The notice does not list specific indoor alternatives."
                ],
                Warnings =
                [
                    "Do not assume the bus schedule has changed unless the school confirms it.",
                    "Outdoor activities should be treated as suspended after 12:30 PM today."
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "Outdoor activities after 12:30 PM are suspended.",
                        SourceText = "all outdoor extracurricular activities scheduled after 12:30 PM today are suspended"
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Students should ask about indoor alternatives.",
                        SourceText = "Students should check with their activity coordinators for indoor alternatives."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Bus changes are unclear.",
                        SourceText = "Bus schedules have not been confirmed to change."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "Parents should watch for updates.",
                        SourceText = "Parents should monitor the school portal for further updates."
                    }
                ]
            };
        }

        private static CrisisActionAnalysisResult BuildChineseResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "下午 12:30 后户外活动暂停",
                Summary = "由于极端天气，今天下午 12:30 后安排的户外课外活动暂停。学生应联系活动负责人确认是否有室内替代安排，家长应继续查看学校 portal 的更新。",
                Priority = "high",
                ImportantPoints =
                [
                    "今天下午 12:30 后的户外课外活动暂停。",
                    "可能有室内替代安排，但需要向活动负责人确认。",
                    "校车时间是否改变尚未确认。",
                    "家长需要关注学校 portal 后续通知。"
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "联系活动负责人，确认是否有室内替代安排。",
                        Deadline = "今天，原定下午 12:30 后活动开始前",
                        Location = "学校或活动负责人联系渠道",
                        RequiredDocuments = []
                    },
                    new ActionItem
                    {
                        Task = "查看学校 portal 是否发布进一步更新。",
                        Deadline = "今天以及学校发布新通知前",
                        Location = "学校 portal",
                        RequiredDocuments = []
                    }
                ],
                UnclearItems =
                [
                    "通知没有确认校车安排是否改变。",
                    "通知没有列出具体室内替代活动。"
                ],
                Warnings =
                [
                    "不要在学校确认前假设校车时间已经改变。",
                    "今天下午 12:30 后的户外活动应视为暂停。"
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "下午 12:30 后户外活动暂停。",
                        SourceText = "all outdoor extracurricular activities scheduled after 12:30 PM today are suspended"
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "学生应确认室内替代安排。",
                        SourceText = "Students should check with their activity coordinators for indoor alternatives."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "校车安排仍不明确。",
                        SourceText = "Bus schedules have not been confirmed to change."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "家长应查看学校 portal。",
                        SourceText = "Parents should monitor the school portal for further updates."
                    }
                ]
            };
        }
    }
}

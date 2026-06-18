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
                ClearBridgeOutputLanguages.SimplifiedChinese => BuildChineseResult(),
                ClearBridgeOutputLanguages.Arabic => BuildArabicResult(),
                _ => BuildEnglishResult()
            };
            return Task.FromResult(result);
        }

        private static CrisisActionAnalysisResult BuildEnglishResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "Worksheet due through Google Classroom",
                Summary = "The selected captions say that a community service worksheet should be submitted by Friday through Google Classroom. The speaker also says one reading reference is only an example, not a new assignment.",
                Priority = "medium",
                ImportantPoints =
                [
                    "A community service worksheet is being discussed.",
                    "The worksheet has a Friday deadline.",
                    "The required submission location is Google Classroom.",
                    "The older project reference is an example, not a new assignment."
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "Submit the community service worksheet.",
                        Deadline = "Friday",
                        Location = "Google Classroom",
                        RequiredDocuments = ["community service worksheet"]
                    }
                ],
                UnclearItems =
                [
                    "The captions do not provide an exact time on Friday.",
                    "The captions do not say whether the due date has already changed."
                ],
                Warnings =
                [
                    "Do not treat the reading example about last year's project as a new assignment."
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "The worksheet is due Friday through Google Classroom.",
                        SourceText = "Please submit the worksheet by Friday through Google Classroom."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "The prior project reference is only an example.",
                        SourceText = "The reading example about last year's project is only an example, not a new assignment."
                    }
                ]
            };
        }

        private static CrisisActionAnalysisResult BuildChineseResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "Worksheet 需在周五通过 Google Classroom 提交",
                Summary = "所选字幕说明，需要在周五前通过 Google Classroom 提交 community service worksheet。发言人还说明，关于去年项目的阅读内容只是示例，不是新作业。",
                Priority = "medium",
                ImportantPoints =
                [
                    "课堂正在讨论 community service worksheet。",
                    "worksheet 的截止日期是周五。",
                    "提交位置是 Google Classroom。",
                    "去年项目的阅读内容只是示例，不是新作业。"
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "提交 community service worksheet。",
                        Deadline = "周五",
                        Location = "Google Classroom",
                        RequiredDocuments = ["community service worksheet"]
                    }
                ],
                UnclearItems =
                [
                    "字幕没有说明周五的具体截止时间。",
                    "字幕没有说明截止日期是否已经发生变化。"
                ],
                Warnings =
                [
                    "不要把去年项目的阅读示例当成新的作业要求。"
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "worksheet 需要周五通过 Google Classroom 提交。",
                        SourceText = "Please submit the worksheet by Friday through Google Classroom."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "去年项目内容只是示例。",
                        SourceText = "The reading example about last year's project is only an example, not a new assignment."
                    }
                ]
            };
        }

        private static CrisisActionAnalysisResult BuildArabicResult()
        {
            return new CrisisActionAnalysisResult
            {
                Title = "ورقة العمل مطلوبة عبر Google Classroom بحلول الجمعة",
                Summary = "توضح التسميات المحددة أن ورقة عمل خدمة المجتمع يجب أن ترسل بحلول الجمعة عبر Google Classroom. ويوضح المتحدث أن مثال القراءة عن مشروع العام الماضي هو مثال فقط وليس واجبا جديدا.",
                Priority = "medium",
                ImportantPoints =
                [
                    "النقاش يدور حول ورقة عمل خدمة المجتمع.",
                    "الموعد النهائي لورقة العمل هو الجمعة.",
                    "مكان التسليم هو Google Classroom.",
                    "إشارة مشروع العام الماضي مثال فقط وليست واجبا جديدا."
                ],
                Actions =
                [
                    new ActionItem
                    {
                        Task = "إرسال ورقة عمل خدمة المجتمع.",
                        Deadline = "الجمعة",
                        Location = "Google Classroom",
                        RequiredDocuments = ["community service worksheet"]
                    }
                ],
                UnclearItems =
                [
                    "لا تذكر التسميات وقتا محددا يوم الجمعة.",
                    "لا توضح التسميات ما إذا كان الموعد النهائي قد تغير بالفعل."
                ],
                Warnings =
                [
                    "لا تعامل مثال القراءة عن مشروع العام الماضي كواجب جديد."
                ],
                SourceEvidence =
                [
                    new SourceEvidenceItem
                    {
                        Claim = "ورقة العمل مطلوبة يوم الجمعة عبر Google Classroom.",
                        SourceText = "Please submit the worksheet by Friday through Google Classroom."
                    },
                    new SourceEvidenceItem
                    {
                        Claim = "إشارة المشروع السابق مثال فقط.",
                        SourceText = "The reading example about last year's project is only an example, not a new assignment."
                    }
                ]
            };
        }
    }
}

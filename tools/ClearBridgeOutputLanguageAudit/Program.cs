using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.ClearBridge;

var checks = new List<(string Name, Action Test)>
{
    ("Prompt maps zh-CN to Simplified Chinese", TestPromptLanguageName),
    ("Prompt separates JSON keys and user-visible values", TestPromptKeyRule),
    ("Retry prompt preserves selected language", TestRetryPrompt),
    ("Chinese result with Arabic source evidence passes", TestChinesePassesWithArabicEvidence),
    ("Arabic values are rejected for Chinese output", TestArabicRejectedForChinese),
    ("English result with Chinese source evidence passes", TestEnglishPassesWithChineseEvidence),
    ("Chinese values are rejected for English output", TestChineseRejectedForEnglish),
    ("Arabic result with English source evidence passes", TestArabicPassesWithEnglishEvidence),
    ("Translated JSON keys are rejected after parsing defaults", TestTranslatedKeysRejected),
    ("Rolling summary Chinese output passes", TestRollingChinesePasses),
    ("Rolling summary wrong language is rejected", TestRollingWrongLanguageRejected),
    ("Source evidence text is excluded from language scoring", TestSourceTextLanguageException)
};

var failed = 0;
foreach (var (name, test) in checks)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Console.WriteLine($"FAIL ClearBridge output language audit completed with {failed} failure(s).");
    return 1;
}

Console.WriteLine($"PASS ClearBridge output language audit completed {checks.Count} checks.");
return 0;

static void TestPromptLanguageName()
{
    var prompt = CrisisActionPromptBuilder.BuildSystemPrompt("zh-CN");
    AssertContains(prompt, "The selected output language is: Simplified Chinese");
    AssertDoesNotContain(prompt, "The selected output language is: zh-CN");
}

static void TestPromptKeyRule()
{
    var prompt = CrisisActionPromptBuilder.BuildCaptionSystemPrompt(ClearBridgeOutputLanguages.Arabic);
    AssertContains(prompt, "Keep JSON property names in English snake_case");
    AssertContains(prompt, "All user-visible JSON string values must be written in Arabic");
    AssertContains(prompt, "source_evidence.source_text");
}

static void TestRetryPrompt()
{
    var retry = CrisisActionPromptBuilder.BuildLanguageRetryUserPrompt(
        CrisisActionPromptBuilder.BuildUserPrompt("Synthetic notice text."),
        ClearBridgeOutputLanguages.SimplifiedChinese);

    AssertContains(retry, "previous response did not follow the required output language");
    AssertContains(retry, "Rewrite all user-visible values in Simplified Chinese");
    AssertContains(retry, "Do not translate source_evidence.source_text");
}

static void TestChinesePassesWithArabicEvidence()
{
    ClearBridgeOutputLanguageValidator.EnsureCrisisResultMatches(
        BuildChineseCrisisResult("يرجى إرسال النموذج قبل يوم الجمعة."),
        ClearBridgeOutputLanguages.SimplifiedChinese);
}

static void TestArabicRejectedForChinese()
{
    ExpectLanguageMismatch(
        () => ClearBridgeOutputLanguageValidator.EnsureCrisisResultMatches(
            BuildArabicCrisisResult("يرجى إرسال النموذج قبل يوم الجمعة."),
            ClearBridgeOutputLanguages.SimplifiedChinese));
}

static void TestEnglishPassesWithChineseEvidence()
{
    ClearBridgeOutputLanguageValidator.EnsureCrisisResultMatches(
        BuildEnglishCrisisResult("请在周五前提交表格。"),
        ClearBridgeOutputLanguages.English);
}

static void TestChineseRejectedForEnglish()
{
    ExpectLanguageMismatch(
        () => ClearBridgeOutputLanguageValidator.EnsureCrisisResultMatches(
            BuildChineseCrisisResult("请在周五前提交表格。"),
            ClearBridgeOutputLanguages.English));
}

static void TestArabicPassesWithEnglishEvidence()
{
    ClearBridgeOutputLanguageValidator.EnsureCrisisResultMatches(
        BuildArabicCrisisResult("Please submit the worksheet by Friday."),
        ClearBridgeOutputLanguages.Arabic);
}

static void TestTranslatedKeysRejected()
{
    var parsed = CrisisActionJsonParser.Parse("""
        {
          "标题": "学校通知",
          "摘要": "请在周五前提交表格。"
        }
        """);

    ExpectLanguageMismatch(
        () => ClearBridgeOutputLanguageValidator.EnsureCrisisResultMatches(
            parsed,
            ClearBridgeOutputLanguages.English));
}

static void TestRollingChinesePasses()
{
    ClearBridgeOutputLanguageValidator.EnsureRollingResultMatches(
        BuildChineseRollingResult("Please submit the worksheet by Friday."),
        ClearBridgeOutputLanguages.SimplifiedChinese);
}

static void TestRollingWrongLanguageRejected()
{
    ExpectLanguageMismatch(
        () => ClearBridgeOutputLanguageValidator.EnsureRollingResultMatches(
            BuildArabicRollingResult("Please submit the worksheet by Friday."),
            ClearBridgeOutputLanguages.SimplifiedChinese));
}

static void TestSourceTextLanguageException()
{
    var result = BuildChineseCrisisResult(
        "هذا نص عربي طويل يجب أن يبقى في source_text ولا يجب أن يؤثر على فحص لغة الحقول المرئية.");
    Assert(
        ClearBridgeOutputLanguageValidator.CrisisResultMatches(
            result,
            ClearBridgeOutputLanguages.SimplifiedChinese),
        "Arabic source_text should not make a Chinese result fail.");
}

static CrisisActionAnalysisResult BuildChineseCrisisResult(string sourceText)
{
    return new CrisisActionAnalysisResult
    {
        Title = "学校通知行动计划",
        Summary = "这份通知要求学生在周五前提交表格，并提醒用户在行动前确认原文证据。",
        Priority = "medium",
        ImportantPoints =
        [
            "需要在周五前完成提交。",
            "提交位置和所需材料应根据原文确认。"
        ],
        Actions =
        [
            new ActionItem
            {
                Task = "提交学校表格。",
                Deadline = "周五",
                Location = "Google Classroom",
                RequiredDocuments = ["worksheet"]
            }
        ],
        UnclearItems = ["通知没有说明周五的具体截止时间。"],
        Warnings = ["行动前请核对原文证据。"],
        SourceEvidence =
        [
            new SourceEvidenceItem
            {
                Claim = "通知要求提交表格。",
                SourceText = sourceText
            }
        ]
    };
}

static CrisisActionAnalysisResult BuildEnglishCrisisResult(string sourceText)
{
    return new CrisisActionAnalysisResult
    {
        Title = "School notice action plan",
        Summary = "The notice asks the student to submit the worksheet by Friday and review the source evidence before acting.",
        Priority = "medium",
        ImportantPoints =
        [
            "A worksheet submission is required.",
            "The deadline is Friday."
        ],
        Actions =
        [
            new ActionItem
            {
                Task = "Submit the worksheet.",
                Deadline = "Friday",
                Location = "Google Classroom",
                RequiredDocuments = ["worksheet"]
            }
        ],
        UnclearItems = ["The exact time on Friday is not provided."],
        Warnings = ["Confirm the source evidence before acting."],
        SourceEvidence =
        [
            new SourceEvidenceItem
            {
                Claim = "The notice asks for a worksheet submission.",
                SourceText = sourceText
            }
        ]
    };
}

static CrisisActionAnalysisResult BuildArabicCrisisResult(string sourceText)
{
    return new CrisisActionAnalysisResult
    {
        Title = "خطة عمل لإشعار المدرسة",
        Summary = "يطلب الإشعار من الطالب إرسال ورقة العمل قبل يوم الجمعة ومراجعة الدليل الأصلي قبل التصرف.",
        Priority = "medium",
        ImportantPoints =
        [
            "يجب إرسال ورقة العمل.",
            "الموعد النهائي هو يوم الجمعة."
        ],
        Actions =
        [
            new ActionItem
            {
                Task = "أرسل ورقة العمل.",
                Deadline = "الجمعة",
                Location = "Google Classroom",
                RequiredDocuments = ["worksheet"]
            }
        ],
        UnclearItems = ["لا يذكر الإشعار الوقت المحدد يوم الجمعة."],
        Warnings = ["راجع الدليل الأصلي قبل التصرف."],
        SourceEvidence =
        [
            new SourceEvidenceItem
            {
                Claim = "يطلب الإشعار إرسال ورقة العمل.",
                SourceText = sourceText
            }
        ]
    };
}

static RollingSummaryResult BuildChineseRollingResult(string sourceText)
{
    return new RollingSummaryResult
    {
        CurrentTopic = "学校作业更新",
        BatchSummary = "本批字幕说明学生需要提交 worksheet，并提醒用户确认截止时间。",
        KeyPoints = ["学生需要提交 worksheet。", "截止时间需要根据原文确认。"],
        NewActions =
        [
            new ActionItem
            {
                Task = "提交 worksheet。",
                Deadline = "周五",
                Location = "Google Classroom"
            }
        ],
        DatesAndDeadlines = ["周五"],
        Locations = ["Google Classroom"],
        Warnings = ["保存前请确认字幕证据。"],
        UnresolvedQuestions = ["没有说明周五的具体时间。"],
        SourceEvidence =
        [
            new SourceEvidenceItem
            {
                Claim = "字幕说明需要提交 worksheet。",
                SourceText = sourceText
            }
        ],
        ContextCache = new RollingContextCache
        {
            CurrentTopic = "学校作业更新",
            EstablishedFacts = ["学生需要提交 worksheet。"],
            ConfirmedActions = ["提交 worksheet。"],
            DatesAndDeadlines = ["周五"],
            Locations = ["Google Classroom"],
            Warnings = ["保存前请确认字幕证据。"],
            UnresolvedQuestions = ["没有说明周五的具体时间。"],
            CompressedNarrative = "本批字幕说明学生需要提交 worksheet，并提醒用户确认截止时间。"
        }
    };
}

static RollingSummaryResult BuildArabicRollingResult(string sourceText)
{
    return new RollingSummaryResult
    {
        CurrentTopic = "تحديث واجب مدرسي",
        BatchSummary = "توضح هذه الدفعة أن الطالب يحتاج إلى إرسال ورقة العمل ومراجعة الموعد النهائي.",
        KeyPoints = ["يحتاج الطالب إلى إرسال ورقة العمل.", "يجب تأكيد الموعد من النص الأصلي."],
        NewActions =
        [
            new ActionItem
            {
                Task = "أرسل ورقة العمل.",
                Deadline = "الجمعة",
                Location = "Google Classroom"
            }
        ],
        DatesAndDeadlines = ["الجمعة"],
        Locations = ["Google Classroom"],
        Warnings = ["راجع دليل الترجمة قبل الحفظ."],
        UnresolvedQuestions = ["لم يتم توفير وقت محدد يوم الجمعة."],
        SourceEvidence =
        [
            new SourceEvidenceItem
            {
                Claim = "توضح التسميات الحاجة إلى إرسال ورقة العمل.",
                SourceText = sourceText
            }
        ],
        ContextCache = new RollingContextCache
        {
            CurrentTopic = "تحديث واجب مدرسي",
            EstablishedFacts = ["يحتاج الطالب إلى إرسال ورقة العمل."],
            ConfirmedActions = ["أرسل ورقة العمل."],
            DatesAndDeadlines = ["الجمعة"],
            Locations = ["Google Classroom"],
            Warnings = ["راجع دليل الترجمة قبل الحفظ."],
            UnresolvedQuestions = ["لم يتم توفير وقت محدد يوم الجمعة."],
            CompressedNarrative = "توضح هذه الدفعة أن الطالب يحتاج إلى إرسال ورقة العمل ومراجعة الموعد النهائي."
        }
    };
}

static void ExpectLanguageMismatch(Action action)
{
    try
    {
        action();
    }
    catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "OutputLanguageMismatch")
    {
        return;
    }

    throw new InvalidOperationException("Expected OutputLanguageMismatch.");
}

static void AssertContains(string text, string expected)
{
    Assert(
        text.Contains(expected, StringComparison.Ordinal),
        $"Expected text to contain '{expected}'.");
}

static void AssertDoesNotContain(string text, string unexpected)
{
    Assert(
        !text.Contains(unexpected, StringComparison.Ordinal),
        $"Expected text not to contain '{unexpected}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

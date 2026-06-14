namespace LiveCaptionsTranslator.services.ClearBridge
{
    public sealed class ClearBridgeLocalizationService
    {
        public const string English = "English";
        public const string SimplifiedChinese = "Simplified Chinese";

        public static readonly string[] SupportedUiLanguages =
        [
            English,
            SimplifiedChinese
        ];

        private static readonly Dictionary<string, string> EnglishStrings = new()
        {
            ["ClearBridge.Title"] = "ClearBridge",
            ["ClearBridge.Subtitle"] = "Turn complex notices into a clear action plan.",
            ["ClearBridge.UiLanguage"] = "UI Language",
            ["ClearBridge.Provider"] = "Provider",
            ["ClearBridge.OutputLanguage"] = "Output Language",
            ["ClearBridge.Input"] = "Notice text",
            ["ClearBridge.Input.Placeholder"] = "Paste a school, government, medical, or community notice here.",
            ["ClearBridge.CharacterCount"] = "{0}/{1} characters",
            ["ClearBridge.Example"] = "Example Notice",
            ["ClearBridge.Clear"] = "Clear",
            ["ClearBridge.Analyze"] = "Analyze",
            ["ClearBridge.Cancel"] = "Cancel",
            ["ClearBridge.Status"] = "Status",
            ["ClearBridge.Ready"] = "Ready",
            ["ClearBridge.Analyzing"] = "Analyzing",
            ["ClearBridge.Completed"] = "Completed",
            ["ClearBridge.Failed"] = "Failed",
            ["ClearBridge.Cancelled"] = "Cancelled",
            ["ClearBridge.MockMode"] = "Mock Mode",
            ["ClearBridge.MockMode.Detail"] = "Using fixed demo output. This is not real AI analysis.",
            ["ClearBridge.Fallback.Detail"] = "Provider failed. Showing Mock Mode fallback.",
            ["ClearBridge.SimpleSummary"] = "Simple Summary",
            ["ClearBridge.Priority"] = "Priority",
            ["ClearBridge.ImportantPoints"] = "Important Points",
            ["ClearBridge.Actions"] = "Action Checklist",
            ["ClearBridge.Deadline"] = "Deadline",
            ["ClearBridge.Location"] = "Location",
            ["ClearBridge.RequiredDocuments"] = "Required Documents",
            ["ClearBridge.Warnings"] = "Warnings",
            ["ClearBridge.UnclearItems"] = "Unclear Items",
            ["ClearBridge.SourceEvidence"] = "Source Evidence",
            ["ClearBridge.CopySummary"] = "Copy Summary",
            ["ClearBridge.CopyActionPlan"] = "Copy Action Plan",
            ["ClearBridge.SaveToHistory"] = "Save to History",
            ["ClearBridge.AnalyzeAgain"] = "Analyze Again",
            ["ClearBridge.SavedToHistory"] = "Saved to History",
            ["ClearBridge.Copy.Success"] = "Copied.",
            ["ClearBridge.History.Success"] = "Analysis saved to History.",
            ["ClearBridge.History.Failed"] = "Could not save the result to History.",
            ["ClearBridge.Error.InputEmpty"] = "Paste a notice or message before analyzing.",
            ["ClearBridge.Error.InputTooShort"] = "The text is too short. Paste at least 30 characters.",
            ["ClearBridge.Error.InputTooLong"] = "The text is too long. Please shorten it.",
            ["ClearBridge.Error.OutputLanguageMissing"] = "Choose an output language.",
            ["ClearBridge.Error.ProviderNotConfigured"] = "The selected provider is not configured.",
            ["ClearBridge.Error.ApiKeyMissing"] = "OpenAI-compatible API key is missing.",
            ["ClearBridge.Error.ProviderTimeout"] = "The provider timed out.",
            ["ClearBridge.Error.HttpError"] = "The provider returned an HTTP error.",
            ["ClearBridge.Error.NetworkError"] = "The provider could not be reached.",
            ["ClearBridge.Error.InvalidJson"] = "The provider did not return valid JSON.",
            ["ClearBridge.Error.EmptyResponse"] = "The provider returned an empty response.",
            ["ClearBridge.Error.Clipboard"] = "Could not copy to clipboard.",
            ["ClearBridge.Error.Cancelled"] = "Analysis was cancelled.",
            ["ClearBridge.Error.Unexpected"] = "Something went wrong while analyzing.",
            ["ClearBridge.EmptyList"] = "None found in the source text.",
        };

        private static readonly Dictionary<string, string> ChineseStrings = new()
        {
            ["ClearBridge.Title"] = "ClearBridge",
            ["ClearBridge.Subtitle"] = "把复杂通知整理成清晰行动计划。",
            ["ClearBridge.UiLanguage"] = "界面语言",
            ["ClearBridge.Provider"] = "分析方式",
            ["ClearBridge.OutputLanguage"] = "输出语言",
            ["ClearBridge.Input"] = "通知原文",
            ["ClearBridge.Input.Placeholder"] = "在这里粘贴学校、政府、医疗或社区通知。",
            ["ClearBridge.CharacterCount"] = "{0}/{1} 字符",
            ["ClearBridge.Example"] = "示例通知",
            ["ClearBridge.Clear"] = "清空",
            ["ClearBridge.Analyze"] = "分析",
            ["ClearBridge.Cancel"] = "取消",
            ["ClearBridge.Status"] = "状态",
            ["ClearBridge.Ready"] = "就绪",
            ["ClearBridge.Analyzing"] = "分析中",
            ["ClearBridge.Completed"] = "已完成",
            ["ClearBridge.Failed"] = "失败",
            ["ClearBridge.Cancelled"] = "已取消",
            ["ClearBridge.MockMode"] = "Mock 模式",
            ["ClearBridge.MockMode.Detail"] = "正在使用固定演示结果。这不是真实 AI 分析。",
            ["ClearBridge.Fallback.Detail"] = "真实 Provider 失败，已显示 Mock 模式备用结果。",
            ["ClearBridge.SimpleSummary"] = "简单解释",
            ["ClearBridge.Priority"] = "重要程度",
            ["ClearBridge.ImportantPoints"] = "重要事项",
            ["ClearBridge.Actions"] = "行动清单",
            ["ClearBridge.Deadline"] = "截止时间",
            ["ClearBridge.Location"] = "地点",
            ["ClearBridge.RequiredDocuments"] = "所需材料",
            ["ClearBridge.Warnings"] = "风险提示",
            ["ClearBridge.UnclearItems"] = "不确定项",
            ["ClearBridge.SourceEvidence"] = "原文依据",
            ["ClearBridge.CopySummary"] = "复制摘要",
            ["ClearBridge.CopyActionPlan"] = "复制行动计划",
            ["ClearBridge.SaveToHistory"] = "保存到 History",
            ["ClearBridge.AnalyzeAgain"] = "重新分析",
            ["ClearBridge.SavedToHistory"] = "已保存到 History",
            ["ClearBridge.Copy.Success"] = "已复制。",
            ["ClearBridge.History.Success"] = "分析结果已保存到 History。",
            ["ClearBridge.History.Failed"] = "无法保存分析结果到 History。",
            ["ClearBridge.Error.InputEmpty"] = "请先粘贴通知或消息。",
            ["ClearBridge.Error.InputTooShort"] = "文本太短，请至少输入 30 个字符。",
            ["ClearBridge.Error.InputTooLong"] = "文本太长，请缩短后再试。",
            ["ClearBridge.Error.OutputLanguageMissing"] = "请选择输出语言。",
            ["ClearBridge.Error.ProviderNotConfigured"] = "所选 Provider 尚未配置。",
            ["ClearBridge.Error.ApiKeyMissing"] = "缺少 OpenAI-compatible API Key。",
            ["ClearBridge.Error.ProviderTimeout"] = "Provider 响应超时。",
            ["ClearBridge.Error.HttpError"] = "Provider 返回 HTTP 错误。",
            ["ClearBridge.Error.NetworkError"] = "无法连接 Provider。",
            ["ClearBridge.Error.InvalidJson"] = "Provider 没有返回有效 JSON。",
            ["ClearBridge.Error.EmptyResponse"] = "Provider 返回了空响应。",
            ["ClearBridge.Error.Clipboard"] = "复制到剪贴板失败。",
            ["ClearBridge.Error.Cancelled"] = "分析已取消。",
            ["ClearBridge.Error.Unexpected"] = "分析时出现问题。",
            ["ClearBridge.EmptyList"] = "原文中未发现。",
        };

        public string Language { get; private set; } = English;

        public void SetLanguage(string? language)
        {
            Language = language == SimplifiedChinese ? SimplifiedChinese : English;
        }

        public string T(string key)
        {
            var source = Language == SimplifiedChinese ? ChineseStrings : EnglishStrings;
            return source.TryGetValue(key, out var value)
                ? value
                : EnglishStrings.GetValueOrDefault(key, key);
        }

        public string Format(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }
    }
}

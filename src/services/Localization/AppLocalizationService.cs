using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using Wpf.Ui.Controls;

namespace LiveCaptionsTranslator.services.Localization
{
    public sealed class UiLanguageOption
    {
        public UiLanguageOption(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public string Code { get; }

        public string DisplayName { get; }
    }

    public static class AppLocalizationService
    {
        public const string EnglishCode = "en";
        public const string ChineseCode = "zh-Hans";
        public const string ArabicCode = "ar";

        private static readonly IReadOnlyList<UiLanguageOption> languageOptions =
        [
            new(EnglishCode, "English"),
            new(ChineseCode, "简体中文"),
            new(ArabicCode, "العربية")
        ];

        private static readonly Dictionary<string, Dictionary<string, string>> localizedStrings =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> literalKeys = new(StringComparer.Ordinal)
        {
            ["LiveCaptions Translator"] = "App.Title",
            ["Caption"] = "Nav.Caption",
            ["ClearBridge"] = "Nav.ClearBridge",
            ["Setting"] = "Nav.Setting",
            ["History"] = "Nav.History",
            ["Info"] = "Nav.Info",
            ["Log Cards of Captions"] = "Main.Tooltip.LogCards",
            ["Pause Translation (Log Only)"] = "Main.Tooltip.PauseTranslation",
            ["Overlay Window"] = "Main.Tooltip.OverlayWindow",
            ["Always on Top"] = "Main.Tooltip.AlwaysOnTop",
            ["LiveCaptions"] = "Settings.LiveCaptions",
            ["API Interval"] = "Settings.ApiInterval",
            ["Translate API"] = "Settings.TranslateApi",
            ["Target Language"] = "Settings.TargetLanguage",
            ["API Setting"] = "Settings.ApiSetting",
            ["Open"] = "Settings.Open",
            ["Show Latency"] = "Settings.ShowLatency",
            ["Contexts"] = "Settings.Contexts",
            ["Display Sentences"] = "Settings.DisplaySentences",
            ["Context Aware"] = "Settings.ContextAware",
            ["UI Language"] = "Settings.UiLanguage",
            ["Show"] = "Settings.Show",
            ["Hide"] = "Settings.Hide",
            ["Off"] = "Common.Off",
            ["On"] = "Common.On",
            ["Previous "] = "History.Previous",
            ["Next Page"] = "History.NextPage",
            ["Search"] = "History.Search",
            ["Export"] = "History.Export",
            ["Delete All"] = "History.DeleteAll",
            ["Refresh"] = "History.Refresh",
            ["Time"] = "History.Time",
            ["Translated"] = "History.Translated",
            ["API"] = "History.Api",
            ["Feature"] = "History.Feature",
            ["Click To Copy"] = "Caption.ClickToCopy",
            ["Copied."] = "Common.Copied",
            ["Copy Failed."] = "Common.CopyFailed",
            ["Repository:"] = "Info.Repository",
            ["Wiki:"] = "Info.Wiki",
            ["Maintainer:"] = "Info.Maintainer",
            ["Version:"] = "Info.Version",
            ["Getting Started"] = "Welcome.Title",
            ["Welcome to LiveCaptions Translator!"] = "Welcome.Heading",
            ["I have fully understood and configured."] = "Welcome.Close",
            ["Prompt"] = "SettingsWindow.Prompt",
            ["Current Config: "] = "SettingsWindow.CurrentConfig",
            ["New"] = "SettingsWindow.New",
            ["Delete"] = "SettingsWindow.Delete",
            ["You must keep at least one config."] = "SettingsWindow.KeepOneConfig",
            ["Model Name"] = "SettingsWindow.ModelName",
            ["Temperature"] = "SettingsWindow.Temperature",
            ["API Url"] = "SettingsWindow.ApiUrl",
            ["API Url (Base)"] = "SettingsWindow.ApiUrlBase",
            ["API Key"] = "SettingsWindow.ApiKey",
            ["APP Key"] = "SettingsWindow.AppKey",
            ["APP Secret"] = "SettingsWindow.AppSecret",
            ["APP ID"] = "SettingsWindow.AppId",
            ["Source Language"] = "SettingsWindow.SourceLanguage",
            ["Load Models"] = "SettingsWindow.LoadModels",
            ["Font Size Increase"] = "Overlay.Tooltip.FontSizeIncrease",
            ["Font Size Decrease"] = "Overlay.Tooltip.FontSizeDecrease",
            ["Font Stroke Increase"] = "Overlay.Tooltip.FontStrokeIncrease",
            ["Font Stroke Decrease"] = "Overlay.Tooltip.FontStrokeDecrease",
            ["Font Bold"] = "Overlay.Tooltip.FontBold",
            ["Font Color"] = "Overlay.Tooltip.FontColor",
            ["Background Opacity Increase"] = "Overlay.Tooltip.BackgroundOpacityIncrease",
            ["Background Opacity Decrease"] = "Overlay.Tooltip.BackgroundOpacityDecrease",
            ["Background Color"] = "Overlay.Tooltip.BackgroundColor",
            ["Show only subtitles or translations"] = "Overlay.Tooltip.OnlyMode",
            ["Switch the order of subtitles and translations"] = "Overlay.Tooltip.SwitchMode",
            ["Click Through"] = "Overlay.Tooltip.ClickThrough",
        };

        private static readonly DependencyProperty OriginalTextProperty =
            DependencyProperty.RegisterAttached(
                "OriginalText",
                typeof(string),
                typeof(AppLocalizationService),
                new PropertyMetadata(null));

        private static readonly DependencyProperty OriginalContentProperty =
            DependencyProperty.RegisterAttached(
                "OriginalContent",
                typeof(string),
                typeof(AppLocalizationService),
                new PropertyMetadata(null));

        private static readonly DependencyProperty OriginalToolTipProperty =
            DependencyProperty.RegisterAttached(
                "OriginalToolTip",
                typeof(string),
                typeof(AppLocalizationService),
                new PropertyMetadata(null));

        private static readonly DependencyProperty OriginalPlaceholderProperty =
            DependencyProperty.RegisterAttached(
                "OriginalPlaceholder",
                typeof(string),
                typeof(AppLocalizationService),
                new PropertyMetadata(null));

        private static readonly DependencyProperty OriginalOnContentProperty =
            DependencyProperty.RegisterAttached(
                "OriginalOnContent",
                typeof(string),
                typeof(AppLocalizationService),
                new PropertyMetadata(null));

        private static readonly DependencyProperty OriginalOffContentProperty =
            DependencyProperty.RegisterAttached(
                "OriginalOffContent",
                typeof(string),
                typeof(AppLocalizationService),
                new PropertyMetadata(null));

        private static string currentLanguage = EnglishCode;
        private static bool resourcesLoaded;

        public static IReadOnlyList<UiLanguageOption> SupportedLanguages => languageOptions;

        public static string CurrentLanguage => currentLanguage;

        public static string SavedLanguage => NormalizeLanguageCode(Translator.Setting?.UiLanguage);

        public static bool IsRightToLeft => currentLanguage == ArabicCode;

        public static FlowDirection CurrentFlowDirection =>
            IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        public static void Initialize(string? language)
        {
            EnsureResourcesLoaded();
            currentLanguage = NormalizeLanguageCode(language);
            if (Translator.Setting != null && Translator.Setting.UiLanguage != currentLanguage)
                Translator.Setting.UiLanguage = currentLanguage;
        }

        public static bool SaveLanguageForNextRestart(string? language)
        {
            EnsureResourcesLoaded();
            var normalized = NormalizeLanguageCode(language);
            if (Translator.Setting == null)
                return false;

            if (Translator.Setting.UiLanguage == normalized)
                return false;

            Translator.Setting.UiLanguage = normalized;
            return true;
        }

        public static void SetLanguage(string? language)
        {
            SaveLanguageForNextRestart(language);
        }

        public static string NormalizeLanguageCode(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return EnglishCode;

            var value = language.Trim();
            return value switch
            {
                "English" or "en" or "en-US" => EnglishCode,
                "Simplified Chinese" or "简体中文" or "Chinese" or "zh" or "zh-CN" or "zh-Hans" => ChineseCode,
                "Arabic" or "العربية" or "ar" or "ar-SA" => ArabicCode,
                _ => EnglishCode
            };
        }

        public static string T(string key)
        {
            EnsureResourcesLoaded();

            if (localizedStrings.TryGetValue(currentLanguage, out var current) &&
                current.TryGetValue(key, out var value))
            {
                return value;
            }

            if (localizedStrings.TryGetValue(EnglishCode, out var english) &&
                english.TryGetValue(key, out var fallback))
            {
                return fallback;
            }

            return key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static UiLanguageOption GetLanguageOption(string? language)
        {
            var normalized = NormalizeLanguageCode(language);
            return languageOptions.First(option => option.Code == normalized);
        }

        public static string FeatureTypeLabel(string featureType)
        {
            var normalized = featureType.Replace(" ", string.Empty);
            var key = "Feature." + normalized;
            var label = T(key);
            return label == key ? featureType : label;
        }

        public static void ApplyTo(DependencyObject root)
        {
            if (root is FrameworkElement frameworkElement)
                frameworkElement.FlowDirection = CurrentFlowDirection;

            ApplyKnownText(root);
            ApplyTechnicalLeftToRight(root);
            foreach (var child in GetChildren(root).ToList())
                ApplyTo(child);
        }

        private static void EnsureResourcesLoaded()
        {
            if (resourcesLoaded)
                return;

            LoadResourceFile(EnglishCode, "en.json");
            LoadResourceFile(ChineseCode, "zh-Hans.json");
            LoadResourceFile(ArabicCode, "ar.json");
            resourcesLoaded = true;
        }

        private static void LoadResourceFile(string languageCode, string fileName)
        {
            var basePath = Path.Combine(AppContext.BaseDirectory, "src", "assets", "localization", fileName);
            var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "assets", "localization", fileName);
            var path = File.Exists(basePath) ? basePath : sourcePath;
            if (!File.Exists(path))
            {
                localizedStrings[languageCode] = new Dictionary<string, string>();
                return;
            }

            var json = File.ReadAllText(path);
            localizedStrings[languageCode] =
                JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }

        private static void ApplyKnownText(DependencyObject element)
        {
            if (element is System.Windows.Controls.TextBlock textBlock)
            {
                if (textBlock.Inlines.Count == 0)
                    ApplyTextValue(textBlock, OriginalTextProperty, textBlock.Text, value => textBlock.Text = value);

                foreach (var run in textBlock.Inlines.OfType<Run>().ToList())
                    ApplyTextValue(run, OriginalTextProperty, run.Text, value => run.Text = value);
            }

            if (element is ContentControl contentControl && contentControl.Content is string content)
            {
                ApplyTextValue(
                    contentControl,
                    OriginalContentProperty,
                    content,
                    value => contentControl.Content = value);
            }

            if (element is FrameworkElement frameworkElement && frameworkElement.ToolTip is string toolTip)
            {
                ApplyTextValue(
                    frameworkElement,
                    OriginalToolTipProperty,
                    toolTip,
                    value => frameworkElement.ToolTip = value);
            }

            if (element is AutoSuggestBox autoSuggestBox)
            {
                ApplyTextValue(
                    autoSuggestBox,
                    OriginalPlaceholderProperty,
                    autoSuggestBox.PlaceholderText,
                    value => autoSuggestBox.PlaceholderText = value);
            }

            if (element is ToggleSwitch toggleSwitch)
            {
                if (toggleSwitch.OnContent is string onContent)
                {
                    ApplyTextValue(
                        toggleSwitch,
                        OriginalOnContentProperty,
                        onContent,
                        value => toggleSwitch.OnContent = value);
                }

                if (toggleSwitch.OffContent is string offContent)
                {
                    ApplyTextValue(
                        toggleSwitch,
                        OriginalOffContentProperty,
                        offContent,
                        value => toggleSwitch.OffContent = value);
                }
            }
        }

        private static void ApplyTextValue(
            DependencyObject element,
            DependencyProperty originalProperty,
            string? currentValue,
            Action<string> applyValue)
        {
            if (string.IsNullOrWhiteSpace(currentValue))
                return;

            var original = element.GetValue(originalProperty) as string;
            if (original == null)
            {
                original = currentValue;
                element.SetValue(originalProperty, original);
            }

            var normalized = NormalizeLiteral(original);
            if (literalKeys.TryGetValue(normalized, out var key))
                applyValue(T(key));
        }

        private static string NormalizeLiteral(string value)
        {
            return value.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        }

        private static void ApplyTechnicalLeftToRight(DependencyObject element)
        {
            if (element is not FrameworkElement frameworkElement)
                return;

            var name = frameworkElement.Name ?? string.Empty;
            if (name.Contains("Api", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("API", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Url", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Provider", StringComparison.OrdinalIgnoreCase))
            {
                frameworkElement.FlowDirection = FlowDirection.LeftToRight;
            }
        }

        private static IEnumerable<DependencyObject> GetChildren(DependencyObject parent)
        {
            var seen = new HashSet<DependencyObject>();
            var visualChildCount = 0;

            try
            {
                visualChildCount = VisualTreeHelper.GetChildrenCount(parent);
            }
            catch (InvalidOperationException)
            {
            }

            for (var index = 0; index < visualChildCount; index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (seen.Add(child))
                    yield return child;
            }

            foreach (var logicalChild in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                if (seen.Add(logicalChild))
                    yield return logicalChild;
            }
        }
    }
}

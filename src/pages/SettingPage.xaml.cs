using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Appearance;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.services.Localization;
using LiveCaptionsTranslator.utils;
using Wpf.Ui.Controls;

namespace LiveCaptionsTranslator
{
    public partial class SettingPage : Page
    {
        private static SettingWindow? SettingWindow;
        private bool applyingLocalization;

        public SettingPage()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            DataContext = Translator.Setting;

            UiLanguageBox.ItemsSource = AppLocalizationService.SupportedLanguages;
            UiLanguageBox.SelectedValue = AppLocalizationService.CurrentLanguage;
            AppLocalizationService.LanguageChanged += AppLocalizationService_LanguageChanged;

            Loaded += (s, e) =>
            {
                (App.Current.MainWindow as MainWindow)?.AutoHeightAdjust(maxHeight: (int)App.Current.MainWindow.MinHeight);
                CheckForFirstUse();
                ApplyLocalization();
            };

            TranslateAPIBox.ItemsSource = Translator.Setting?.Configs.Keys;
            TranslateAPIBox.SelectedIndex = 0;

            LoadAPISetting();
            ApplyLocalization();
        }

        private void AppLocalizationService_LanguageChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(ApplyLocalization);
        }

        private void ApplyLocalization()
        {
            applyingLocalization = true;
            UiLanguageBox.SelectedValue = AppLocalizationService.CurrentLanguage;
            AppLocalizationService.ApplyTo(SettingsRoot);
            SetFlyoutText(LiveCaptionsInfoFlyout, "Settings.LiveCaptions.Info");
            SetFlyoutText(FrequencyInfoFlyout, "Settings.ApiInterval.Info");
            SetFlyoutText(TranslateAPIInfoFlyout, "Settings.TranslateApi.Info");
            SetFlyoutText(TargetLangInfoFlyout, "Settings.TargetLanguage.Info", 350);
            SetFlyoutText(CaptionLogMaxInfoFlyout, "Settings.Contexts.Info");
            SetFlyoutText(ContextAwareInfoFlyout, "Settings.ContextAware.Info");
            RefreshLiveCaptionsButtonText();
            applyingLocalization = false;
        }

        private static void SetFlyoutText(Flyout flyout, string key, double width = 300)
        {
            flyout.Content = new System.Windows.Controls.TextBlock
            {
                Width = width,
                Text = AppLocalizationService.T(key),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private void LiveCaptionsButton_click(object sender, RoutedEventArgs e)
        {
            if (Translator.Window == null)
                return;

            bool isHide = Translator.Window.Current.BoundingRectangle == Rect.Empty;
            if (isHide)
            {
                LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);
            }
            else
            {
                LiveCaptionsHandler.HideLiveCaptions(Translator.Window);
            }

            RefreshLiveCaptionsButtonText();
        }

        private void UiLanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (applyingLocalization)
                return;

            if (UiLanguageBox.SelectedValue is string language)
                AppLocalizationService.SetLanguage(language);
        }

        private void TranslateAPIBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAPISetting();
        }

        private void TargetLangBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetLangBox.SelectedItem != null)
                Translator.Setting.TargetLanguage = TargetLangBox.SelectedItem.ToString();
        }

        private void TargetLangBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Translator.Setting.TargetLanguage = TargetLangBox.Text;
        }

        private void APISettingButton_click(object sender, RoutedEventArgs e)
        {
            if (SettingWindow != null && SettingWindow.IsLoaded)
                SettingWindow.Activate();
            else
            {
                SettingWindow = new SettingWindow();
                SettingWindow.Closed += (sender, args) => SettingWindow = null;
                SettingWindow.Show();
            }
        }

        private void Contexts_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.DisplaySentences = Translator.Setting.NumContexts;
        }

        private void DisplaySentences_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.NumContexts = Translator.Setting.DisplaySentences;
            Translator.Caption.OnPropertyChanged("DisplayLogCards");
            Translator.Caption.OnPropertyChanged("OverlayPreviousTranslation");
        }

        private void LiveCaptionsInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Show();
        }

        private void LiveCaptionsInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Hide();
        }

        private void FrequencyInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Show();
        }

        private void FrequencyInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Hide();
        }

        private void TranslateAPIInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Show();
        }

        private void TranslateAPIInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Hide();
        }

        private void TargetLangInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Show();
        }

        private void TargetLangInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Hide();
        }

        private void CaptionLogMaxInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Show();
        }

        private void CaptionLogMaxInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Hide();
        }

        private void ContextAwareInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Show();
        }

        private void ContextAwareInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Hide();
        }

        private void CheckForFirstUse()
        {
            if (Translator.FirstUseFlag)
                ButtonText.Text = AppLocalizationService.T("Settings.Hide");
        }

        private void RefreshLiveCaptionsButtonText()
        {
            if (Translator.Window == null)
                return;

            var isHidden = Translator.Window.Current.BoundingRectangle == Rect.Empty;
            ButtonText.Text = AppLocalizationService.T(isHidden ? "Settings.Show" : "Settings.Hide");
        }

        public void LoadAPISetting()
        {
            var configType = Translator.Setting[Translator.Setting.ApiName].GetType();
            var languagesProp = configType.GetProperty(
                "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            // Traverse base classes to find `SupportedLanguages`
            while (configType != null && languagesProp == null)
            {
                configType = configType.BaseType;
                languagesProp = configType.GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);
            }
            if (languagesProp == null)
                languagesProp = typeof(TranslateAPIConfig).GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            var supportedLanguages = (Dictionary<string, string>)languagesProp.GetValue(null);
            TargetLangBox.ItemsSource = supportedLanguages.Keys;

            string targetLang = Translator.Setting.TargetLanguage;
            if (!supportedLanguages.ContainsKey(targetLang))
                supportedLanguages[targetLang] = targetLang;    // add custom language to supported languages
            TargetLangBox.SelectedItem = targetLang;
        }
    }
}

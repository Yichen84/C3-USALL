using System.Windows;
using System.Diagnostics;
using System.Windows.Navigation;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;

using LiveCaptionsTranslator.services.Localization;

namespace LiveCaptionsTranslator
{
    public partial class WelcomeWindow : FluentWindow
    {
        public WelcomeWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            ApplyLocalization();

            Loaded += (s, e) =>
            {
                SystemThemeWatcher.Watch(
                    this,
                    WindowBackdropType.Mica,
                    true
                );
            };
        }

        private void ApplyLocalization()
        {
            FlowDirection = AppLocalizationService.CurrentFlowDirection;
            Title = AppLocalizationService.T("Welcome.Title");
            WelcomeTitleBarText.Text = AppLocalizationService.T("Welcome.Title");
            WelcomeHeadingText.Text = AppLocalizationService.T("Welcome.Heading");
            WelcomeBodyText.Text = AppLocalizationService.T("Welcome.Body");
            WelcomeCloseButton.Content = AppLocalizationService.T("Welcome.Close");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}

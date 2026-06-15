using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Navigation;
using Wpf.Ui.Appearance;

using LiveCaptionsTranslator.services.Localization;

namespace LiveCaptionsTranslator
{
    public partial class InfoPage : Page
    {
        public const int MIN_HEIGHT = 210;

        public InfoPage()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            Version.Text = version;
            AppLocalizationService.LanguageChanged += AppLocalizationService_LanguageChanged;
            ApplyLocalization();

            Loaded += (s, e) =>
            {
                (App.Current.MainWindow as MainWindow)?.AutoHeightAdjust(minHeight: MIN_HEIGHT, maxHeight: MIN_HEIGHT);
            };
        }

        private void AppLocalizationService_LanguageChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(ApplyLocalization);
        }

        private void ApplyLocalization()
        {
            AppLocalizationService.ApplyTo(InfoRoot);
            ContributionText.Text = AppLocalizationService.T("Info.Contribution");
            RepositoryLabel.Text = AppLocalizationService.T("Info.Repository");
            WikiLabel.Text = AppLocalizationService.T("Info.Wiki");
            MaintainerLabel.Text = AppLocalizationService.T("Info.Maintainer");
            MaintainerJoinRun.Text = AppLocalizationService.T("Info.Maintainer.Join");
            VersionLabel.Text = AppLocalizationService.T("Info.Version");
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Appearance;

using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.ClearBridge;
using LiveCaptionsTranslator.services.Localization;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator
{
    public partial class ClearBridgePage : Page
    {
        public const int MIN_HEIGHT = 720;

        private readonly CrisisActionAnalysisService analysisService = new();
        private readonly ClearBridgeLocalizationService localizer = new();

        private CrisisActionAnalysisOutcome? currentOutcome;
        private CancellationTokenSource? analyzeCancellation;
        private bool historySaved;
        private bool resultCardsUseSingleColumn;
        private MouseWheelEventArgs? forwardedMouseWheelEvent;
        private bool applyingUiLanguage;
        private string currentStateKey = "Ready";
        private string currentStateDetail = string.Empty;

        public ClearBridgePage()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();

            UiLanguageBox.ItemsSource = ClearBridgeLocalizationService.SupportedUiLanguages;
            UiLanguageBox.SelectedValue = AppLocalizationService.CurrentLanguage;
            ProviderBox.ItemsSource = new[] { "Mock", "OpenAI-compatible" };
            ProviderBox.SelectedItem = "Mock";
            OutputLanguageBox.ItemsSource = ClearBridgeOutputLanguages.Supported;
            OutputLanguageBox.SelectedItem = ClearBridgeOutputLanguages.English;
            RegisterMouseWheelForwardingHandlers();
            AppLocalizationService.LanguageChanged += AppLocalizationService_LanguageChanged;

            Loaded += (s, e) =>
            {
                (App.Current.MainWindow as MainWindow)?.AutoHeightAdjust(
                    minHeight: MIN_HEIGHT,
                    maxHeight: MIN_HEIGHT);
            };
            Unloaded += (s, e) =>
                AppLocalizationService.LanguageChanged -= AppLocalizationService_LanguageChanged;

            ApplyLocalization();
            UpdateCharacterCount();
            SetState("Ready");
        }

        private void AppLocalizationService_LanguageChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ApplyLocalization();
                if (currentOutcome != null)
                    RenderResult(currentOutcome);
                RefreshStateText();
            });
        }

        private void UiLanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (applyingUiLanguage)
                return;

            localizer.SetLanguage(UiLanguageBox.SelectedValue as string);
        }

        private void SourceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCharacterCount();
        }

        private void RegisterMouseWheelForwardingHandlers()
        {
            UIElement[] wheelTargets =
            [
                PageContentGrid,
                ResultPanel,
                SummaryCard,
                ImportantWarningsGrid,
                ImportantPointsCard,
                ImportantPointsList,
                WarningsCard,
                WarningsList,
                ActionsCard,
                ActionsList,
                UnclearEvidenceGrid,
                UnclearItemsCard,
                UnclearItemsList,
                EvidenceCard,
                EvidenceList,
                SourceTextBox
            ];

            foreach (var target in wheelTargets)
            {
                target.AddHandler(
                    UIElement.PreviewMouseWheelEvent,
                    new MouseWheelEventHandler(ForwardMouseWheelToPageScrollViewer),
                    handledEventsToo: true);
            }
        }

        private void ForwardMouseWheelToPageScrollViewer(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            if (ReferenceEquals(forwardedMouseWheelEvent, e))
                return;

            forwardedMouseWheelEvent = e;
            PageScrollViewer.ScrollToVerticalOffset(
                Math.Clamp(
                    PageScrollViewer.VerticalOffset - e.Delta,
                    0,
                    PageScrollViewer.ScrollableHeight));
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveResultLayout(e.NewSize.Width);
        }

        private void ExampleButton_Click(object sender, RoutedEventArgs e)
        {
            SourceTextBox.Text = MockCrisisActionAnalysisProvider.SampleNotice;
            ProviderBox.SelectedItem = "Mock";
            SetState("Ready");
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (analyzeCancellation != null)
                return;

            SourceTextBox.Clear();
            currentOutcome = null;
            historySaved = false;
            ResultPanel.Visibility = Visibility.Collapsed;
            SaveHistoryButton.IsEnabled = false;
            SetState("Ready");
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (analyzeCancellation != null)
            {
                analyzeCancellation.Cancel();
                return;
            }

            await AnalyzeAsync();
        }

        private async void CopySummaryButton_Click(object sender, RoutedEventArgs e)
        {
            await CopyToClipboardAsync(BuildSummaryText(), "ClearBridge.Copy.Success");
        }

        private async void CopyActionPlanButton_Click(object sender, RoutedEventArgs e)
        {
            await CopyToClipboardAsync(BuildActionPlanText(), "ClearBridge.Copy.Success");
        }

        private async void SaveHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentOutcome == null)
                return;

            await SaveHistoryAsync(currentOutcome, SourceTextBox.Text, GetSelectedOutputLanguage(), showSuccess: true);
        }

        private async void AnalyzeAgainButton_Click(object sender, RoutedEventArgs e)
        {
            await AnalyzeAsync();
        }

        private async Task AnalyzeAsync()
        {
            var sourceText = SourceTextBox.Text;
            var outputLanguage = GetSelectedOutputLanguage();
            var providerName = GetSelectedProvider();

            analyzeCancellation = new CancellationTokenSource();
            SetAnalyzingUi(true);
            SetState("Analyzing");

            try
            {
                var outcome = await analysisService.AnalyzeAsync(
                    providerName,
                    sourceText,
                    outputLanguage,
                    analyzeCancellation.Token);

                currentOutcome = outcome;
                historySaved = false;
                RenderResult(outcome);

                if (outcome.IsMock)
                {
                    SetState("MockMode", outcome.UsedFallback
                        ? localizer.T("ClearBridge.Fallback.Detail")
                        : localizer.T("ClearBridge.MockMode.Detail"));
                }
                else
                {
                    SetState("Completed");
                }

                await SaveHistoryAsync(outcome, sourceText, outputLanguage, showSuccess: false);
            }
            catch (OperationCanceledException)
            {
                SetState("Cancelled", localizer.T("ClearBridge.Error.Cancelled"));
            }
            catch (ClearBridgeAnalysisException ex)
            {
                SetState("Failed", LocalizeError(ex.ErrorCode));
            }
            catch (Exception)
            {
                SetState("Failed", localizer.T("ClearBridge.Error.Unexpected"));
            }
            finally
            {
                analyzeCancellation?.Dispose();
                analyzeCancellation = null;
                SetAnalyzingUi(false);
            }
        }

        private async Task SaveHistoryAsync(
            CrisisActionAnalysisOutcome outcome,
            string sourceText,
            string outputLanguage,
            bool showSuccess)
        {
            try
            {
                await SQLiteHistoryLogger.LogClearBridgeAnalysis(
                    sourceText,
                    outcome,
                    outputLanguage);

                historySaved = true;
                ApplyHistoryButtonState();

                if (showSuccess)
                    SetState(outcome.IsMock ? "MockMode" : "Completed", localizer.T("ClearBridge.History.Success"));
            }
            catch (Exception)
            {
                SaveHistoryButton.IsEnabled = true;
                SetState("Completed", localizer.T("ClearBridge.History.Failed"));
            }
        }

        private async Task CopyToClipboardAsync(string text, string successKey)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                Clipboard.SetText(text);
                SetState(currentOutcome?.IsMock == true ? "MockMode" : "Completed", localizer.T(successKey));
            }
            catch (Exception)
            {
                SetState("Failed", localizer.T("ClearBridge.Error.Clipboard"));
            }

            await Task.CompletedTask;
        }

        private void RenderResult(CrisisActionAnalysisOutcome outcome)
        {
            var result = outcome.Result;
            ResultPanel.Visibility = Visibility.Visible;

            ResultTitleText.Text = result.Title;
            PriorityText.Text = $"{localizer.T("ClearBridge.Priority")}: {result.Priority}";
            SummaryHeaderText.Text = localizer.T("ClearBridge.SimpleSummary");
            SummaryText.Text = result.Summary;

            ImportantPointsList.ItemsSource = BuildStringRows(result.ImportantPoints);
            WarningsList.ItemsSource = BuildStringRows(result.Warnings);
            UnclearItemsList.ItemsSource = BuildStringRows(result.UnclearItems);
            ActionsList.ItemsSource = result.Actions.Count > 0
                ? result.Actions.Select(action => new ActionItemViewModel(action, localizer)).ToList()
                : new List<ActionItemViewModel>
                {
                    new(new ActionItem { Task = localizer.T("ClearBridge.EmptyList") }, localizer)
                };
            EvidenceList.ItemsSource = result.SourceEvidence.Count > 0
                ? result.SourceEvidence
                : new List<SourceEvidenceItem>
                {
                    new()
                    {
                        Claim = localizer.T("ClearBridge.EmptyList"),
                        SourceText = string.Empty
                    }
                };

            ApplyHistoryButtonState();
            ApplyResponsiveResultLayout(ActualWidth);
        }

        private List<string> BuildStringRows(List<string> values)
        {
            if (values.Count == 0)
                return [localizer.T("ClearBridge.EmptyList")];

            return values.Select(value => "- " + value).ToList();
        }

        private string BuildSummaryText()
        {
            if (currentOutcome == null)
                return string.Empty;

            var result = currentOutcome.Result;
            return string.Join(Environment.NewLine,
                result.Title,
                $"{localizer.T("ClearBridge.Priority")}: {result.Priority}",
                result.Summary);
        }

        private string BuildActionPlanText()
        {
            if (currentOutcome == null)
                return string.Empty;

            var result = currentOutcome.Result;
            var builder = new StringBuilder();
            AppendSection(builder, localizer.T("ClearBridge.ImportantPoints"), result.ImportantPoints);

            builder.AppendLine(localizer.T("ClearBridge.Actions"));
            foreach (var action in result.Actions)
            {
                builder.AppendLine("- " + action.Task);
                if (!string.IsNullOrWhiteSpace(action.Deadline))
                    builder.AppendLine($"  {localizer.T("ClearBridge.Deadline")}: {action.Deadline}");
                if (!string.IsNullOrWhiteSpace(action.Location))
                    builder.AppendLine($"  {localizer.T("ClearBridge.Location")}: {action.Location}");
                if (action.RequiredDocuments.Count > 0)
                {
                    builder.AppendLine(
                        $"  {localizer.T("ClearBridge.RequiredDocuments")}: {string.Join(", ", action.RequiredDocuments)}");
                }
            }
            builder.AppendLine();

            AppendSection(builder, localizer.T("ClearBridge.Warnings"), result.Warnings);
            AppendSection(builder, localizer.T("ClearBridge.UnclearItems"), result.UnclearItems);
            return builder.ToString().Trim();
        }

        private static void AppendSection(StringBuilder builder, string title, IEnumerable<string> values)
        {
            builder.AppendLine(title);
            foreach (var value in values)
                builder.AppendLine("- " + value);
            builder.AppendLine();
        }

        private void ApplyLocalization()
        {
            applyingUiLanguage = true;
            FlowDirection = AppLocalizationService.CurrentFlowDirection;
            PageContentGrid.FlowDirection = AppLocalizationService.CurrentFlowDirection;
            ProviderBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            OutputLanguageBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            UiLanguageBox.SelectedValue = AppLocalizationService.CurrentLanguage;
            applyingUiLanguage = false;

            TitleText.Text = localizer.T("ClearBridge.Title");
            SubtitleText.Text = localizer.T("ClearBridge.Subtitle");
            UiLanguageLabel.Text = localizer.T("ClearBridge.UiLanguage");
            ProviderLabel.Text = localizer.T("ClearBridge.Provider");
            OutputLanguageLabel.Text = localizer.T("ClearBridge.OutputLanguage");
            InputLabel.Text = localizer.T("ClearBridge.Input");
            SourceTextBox.ToolTip = localizer.T("ClearBridge.Input.Placeholder");
            ExampleButton.Content = localizer.T("ClearBridge.Example");
            ClearButton.Content = localizer.T("ClearBridge.Clear");
            AnalyzeButton.Content = analyzeCancellation == null
                ? localizer.T("ClearBridge.Analyze")
                : localizer.T("ClearBridge.Cancel");
            StatusTitleText.Text = localizer.T("ClearBridge.Status");
            ImportantPointsHeaderText.Text = localizer.T("ClearBridge.ImportantPoints");
            WarningsHeaderText.Text = localizer.T("ClearBridge.Warnings");
            ActionsHeaderText.Text = localizer.T("ClearBridge.Actions");
            UnclearItemsHeaderText.Text = localizer.T("ClearBridge.UnclearItems");
            EvidenceHeaderText.Text = localizer.T("ClearBridge.SourceEvidence");
            CopySummaryButton.Content = localizer.T("ClearBridge.CopySummary");
            CopyActionPlanButton.Content = localizer.T("ClearBridge.CopyActionPlan");
            ApplyHistoryButtonState();
            AnalyzeAgainButton.Content = localizer.T("ClearBridge.AnalyzeAgain");
            UpdateCharacterCount();
        }

        private void SetAnalyzingUi(bool isAnalyzing)
        {
            AnalyzeButton.Content = isAnalyzing
                ? localizer.T("ClearBridge.Cancel")
                : localizer.T("ClearBridge.Analyze");
            SourceTextBox.IsEnabled = !isAnalyzing;
            ProviderBox.IsEnabled = !isAnalyzing;
            OutputLanguageBox.IsEnabled = !isAnalyzing;
            UiLanguageBox.IsEnabled = !isAnalyzing;
            ExampleButton.IsEnabled = !isAnalyzing;
            ClearButton.IsEnabled = !isAnalyzing;
            CopySummaryButton.IsEnabled = !isAnalyzing;
            CopyActionPlanButton.IsEnabled = !isAnalyzing;
            SaveHistoryButton.IsEnabled = !isAnalyzing && currentOutcome != null && !historySaved;
            AnalyzeAgainButton.IsEnabled = !isAnalyzing;
            ApplyHistoryButtonState();
        }

        private void ApplyHistoryButtonState()
        {
            if (SaveHistoryButton == null)
                return;

            SaveHistoryButton.Content = historySaved
                ? localizer.T("ClearBridge.SavedToHistory")
                : localizer.T("ClearBridge.SaveToHistory");
            SaveHistoryButton.ToolTip = historySaved
                ? localizer.T("ClearBridge.SavedToHistory.Detail")
                : localizer.T("ClearBridge.SaveToHistory.Detail");

            if (currentOutcome == null || historySaved)
                SaveHistoryButton.IsEnabled = false;
        }

        private void ApplyResponsiveResultLayout(double width)
        {
            var useSingleColumn = width > 0 && width < 760;
            if (useSingleColumn == resultCardsUseSingleColumn)
                return;

            resultCardsUseSingleColumn = useSingleColumn;
            ApplyResponsivePairLayout(
                ImportantWarningsGapColumn,
                ImportantWarningsGapRow,
                WarningsCard,
                useSingleColumn);
            ApplyResponsivePairLayout(
                UnclearEvidenceGapColumn,
                UnclearEvidenceGapRow,
                EvidenceCard,
                useSingleColumn);
        }

        private static void ApplyResponsivePairLayout(
            ColumnDefinition gapColumn,
            RowDefinition gapRow,
            FrameworkElement secondCard,
            bool useSingleColumn)
        {
            if (useSingleColumn)
            {
                gapColumn.Width = new GridLength(0);
                gapRow.Height = new GridLength(12);
                Grid.SetColumn(secondCard, 0);
                Grid.SetRow(secondCard, 2);
            }
            else
            {
                gapColumn.Width = new GridLength(12);
                gapRow.Height = new GridLength(0);
                Grid.SetColumn(secondCard, 2);
                Grid.SetRow(secondCard, 0);
            }
        }

        private void SetState(string stateKey, string detail = "")
        {
            currentStateKey = stateKey;
            currentStateDetail = detail;
            RefreshStateText();
        }

        private void RefreshStateText()
        {
            var stateKey = currentStateKey;
            var detail = currentStateDetail;
            if (stateKey == "MockMode" && currentOutcome != null)
            {
                detail = currentOutcome.UsedFallback
                    ? localizer.T("ClearBridge.Fallback.Detail")
                    : localizer.T("ClearBridge.MockMode.Detail");
            }

            var label = localizer.T("ClearBridge." + stateKey);
            StatusText.Text = string.IsNullOrWhiteSpace(detail)
                ? label
                : $"{label}: {detail}";
        }

        private string LocalizeError(string errorCode)
        {
            return localizer.T("ClearBridge.Error." + errorCode);
        }

        private void UpdateCharacterCount()
        {
            CharacterCountText.Text = localizer.Format(
                "ClearBridge.CharacterCount",
                SourceTextBox.Text.Length,
                CrisisActionAnalysisService.MaxInputLength);
        }

        private string GetSelectedProvider()
        {
            return ProviderBox.SelectedItem as string ?? string.Empty;
        }

        private string GetSelectedOutputLanguage()
        {
            return OutputLanguageBox.SelectedItem as string ?? string.Empty;
        }

        private sealed class ActionItemViewModel
        {
            private readonly ActionItem action;

            public ActionItemViewModel(ActionItem action, ClearBridgeLocalizationService localizer)
            {
                this.action = action;
                Task = action.Task;
                DeadlineLine = string.IsNullOrWhiteSpace(action.Deadline)
                    ? string.Empty
                    : $"{localizer.T("ClearBridge.Deadline")}: {action.Deadline}";
                LocationLine = string.IsNullOrWhiteSpace(action.Location)
                    ? string.Empty
                    : $"{localizer.T("ClearBridge.Location")}: {action.Location}";
                RequiredDocumentsLine = action.RequiredDocuments.Count == 0
                    ? string.Empty
                    : $"{localizer.T("ClearBridge.RequiredDocuments")}: {string.Join(", ", action.RequiredDocuments)}";
            }

            public string Task { get; }

            public string DeadlineLine { get; }

            public string LocationLine { get; }

            public string RequiredDocumentsLine { get; }

            public bool Completed
            {
                get => action.Completed;
                set => action.Completed = value;
            }
        }
    }
}

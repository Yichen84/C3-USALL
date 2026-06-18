using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.Localization;

namespace LiveCaptionsTranslator
{
    public partial class RollingSummaryOverlayWindow : Window
    {
        private readonly ObservableCollection<RollingSummaryBatchViewModel> batches = [];
        private double expandedHeight = 620;
        private bool isCollapsed;

        public RollingSummaryOverlayWindow()
        {
            InitializeComponent();
            BatchesList.ItemsSource = batches;
            ApplyLocalization();
            FlowDirection = AppLocalizationService.CurrentFlowDirection;

            Loaded += RollingSummaryOverlayWindow_Loaded;
            Closed += RollingSummaryOverlayWindow_Closed;
        }

        private CaptionPage? CaptionPage => CaptionPage.Instance;

        private void RollingSummaryOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (CaptionPage != null)
            {
                CaptionPage.RollingSummaryStateChanged += CaptionPage_RollingSummaryStateChanged;
                Refresh(CaptionPage.GetRollingSummaryDisplayState());
            }
        }

        private void RollingSummaryOverlayWindow_Closed(object? sender, EventArgs e)
        {
            if (CaptionPage != null)
                CaptionPage.RollingSummaryStateChanged -= CaptionPage_RollingSummaryStateChanged;
        }

        private void CaptionPage_RollingSummaryStateChanged(object? sender, RollingSummaryDisplayState state)
        {
            Dispatcher.Invoke(() => Refresh(state));
        }

        private void ApplyLocalization()
        {
            Title = AppLocalizationService.T("Caption.RollingSummary.Overlay.Title");
            TitleText.Text = AppLocalizationService.T("Caption.RollingSummary.Overlay.Title");
            EmptyText.Text = AppLocalizationService.T("Caption.RollingSummary.Overlay.Empty");

            StartButton.Content = AppLocalizationService.T("Caption.RollingSummary.Start");
            PauseButton.Content = AppLocalizationService.T("Caption.RollingSummary.Pause");
            ResumeButton.Content = AppLocalizationService.T("Caption.RollingSummary.Resume");
            ProcessNowButton.Content = AppLocalizationService.T("Caption.RollingSummary.ProcessNow");
            StopButton.Content = AppLocalizationService.T("Caption.RollingSummary.Stop");
            SaveButton.Content = AppLocalizationService.T("Caption.RollingSummary.SaveConfirmedSummary");
            ClearButton.Content = AppLocalizationService.T("Caption.RollingSummary.ClearTemporaryContext");
            FullReviewButton.Content = AppLocalizationService.T("Caption.RollingSummary.OpenFullReview");

            TopmostButton.ToolTip = AppLocalizationService.T("Caption.RollingSummary.Overlay.Topmost");
            CollapseButton.ToolTip = AppLocalizationService.T("Caption.RollingSummary.Overlay.Collapse");
            CloseButton.ToolTip = AppLocalizationService.T("Caption.RollingSummary.Overlay.Close");
        }

        private void Refresh(RollingSummaryDisplayState state)
        {
            var previousCount = batches.Count;
            var previousOffset = BatchesScrollViewer.VerticalOffset;
            var shouldScrollToBottom = IsNearBottom();

            StatusText.Text = BuildStatusText(state);
            StartButton.IsEnabled = !state.IsProcessing && !state.IsRunning;
            PauseButton.IsEnabled = !state.IsProcessing && state.IsRunning && !state.IsPaused;
            ResumeButton.IsEnabled = !state.IsProcessing && state.IsPaused;
            ProcessNowButton.IsEnabled = !state.IsProcessing && state.IsRunning && !state.IsPaused;
            StopButton.IsEnabled = !state.IsProcessing && state.IsRunning;
            SaveButton.IsEnabled = state.CanSave;
            ClearButton.IsEnabled = !state.IsProcessing;

            batches.Clear();
            foreach (var outcome in state.Outcomes)
                batches.Add(RollingSummaryBatchViewModel.FromOutcome(outcome));

            if (!isCollapsed)
            {
                EmptyText.Visibility = batches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                BatchesScrollViewer.Visibility = batches.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            if (batches.Count > previousCount && shouldScrollToBottom)
            {
                Dispatcher.BeginInvoke(new Action(() => BatchesScrollViewer.ScrollToEnd()));
            }
            else if (!shouldScrollToBottom)
            {
                Dispatcher.BeginInvoke(new Action(() => BatchesScrollViewer.ScrollToVerticalOffset(previousOffset)));
            }
        }

        private string BuildStatusText(RollingSummaryDisplayState state)
        {
            var status = AppLocalizationService.T("Caption.RollingSummary.Status." + state.Status);
            if (state.CanSave)
                status = AppLocalizationService.T("Caption.RollingSummary.ReviewBeforeSaving");
            else if (state.IsSaved)
                status = AppLocalizationService.T("Caption.RollingSummary.Overlay.Saved");

            return string.IsNullOrWhiteSpace(state.Detail)
                ? status
                : status + " · " + state.Detail;
        }

        private bool IsNearBottom()
        {
            return BatchesScrollViewer.ScrollableHeight <= 0 ||
                BatchesScrollViewer.VerticalOffset >= BatchesScrollViewer.ScrollableHeight - 24;
        }

        private void OverlayRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BatchesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            BatchesScrollViewer.ScrollToVerticalOffset(BatchesScrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            TopmostButton.Opacity = Topmost ? 1.0 : 0.55;
        }

        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            isCollapsed = !isCollapsed;
            if (isCollapsed)
            {
                expandedHeight = Math.Max(Height, MinHeight);
                ContentRow.Height = new GridLength(0);
                FooterRow.Height = new GridLength(0);
                BatchesScrollViewer.Visibility = Visibility.Collapsed;
                EmptyText.Visibility = Visibility.Collapsed;
                FooterPanel.Visibility = Visibility.Collapsed;
                Height = 172;
            }
            else
            {
                ContentRow.Height = new GridLength(1, GridUnitType.Star);
                FooterRow.Height = GridLength.Auto;
                FooterPanel.Visibility = Visibility.Visible;
                Height = Math.Max(expandedHeight, MinHeight);
                Refresh(CaptionPage?.GetRollingSummaryDisplayState() ?? new RollingSummaryDisplayState());
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            CaptionPage?.StartRollingSummary();
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            CaptionPage?.PauseRollingSummary();
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            CaptionPage?.ResumeRollingSummary();
        }

        private async void ProcessNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (CaptionPage != null)
                await CaptionPage.ProcessRollingSummaryNowAsync();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            CaptionPage?.StopRollingSummary();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (CaptionPage != null)
                await CaptionPage.SaveRollingSummaryAsync();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            CaptionPage?.ClearRollingSummaryTemporaryContext();
        }

        private void FullReviewButton_Click(object sender, RoutedEventArgs e)
        {
            CaptionPage?.OpenRollingSummaryFullReview();
        }

        private sealed class RollingSummaryBatchViewModel
        {
            public string Header { get; init; } = string.Empty;

            public string Topic { get; init; } = string.Empty;

            public string Summary { get; init; } = string.Empty;

            public string ActionsHeader { get; init; } = string.Empty;

            public string Actions { get; init; } = string.Empty;

            public string DatesHeader { get; init; } = string.Empty;

            public string Dates { get; init; } = string.Empty;

            public string WarningsHeader { get; init; } = string.Empty;

            public string Warnings { get; init; } = string.Empty;

            public string UnresolvedHeader { get; init; } = string.Empty;

            public string Unresolved { get; init; } = string.Empty;

            public static RollingSummaryBatchViewModel FromOutcome(RollingSummaryOutcome outcome)
            {
                var result = outcome.Result;
                return new RollingSummaryBatchViewModel
                {
                    Header = string.Format(
                        AppLocalizationService.T("Caption.RollingSummary.Overlay.BatchHeader"),
                        outcome.Request.BatchNumber,
                        outcome.CompletedAt.ToLocalTime().ToString("HH:mm:ss")),
                    Topic = AppLocalizationService.T("Caption.RollingSummary.CurrentTopic") + ": " + result.CurrentTopic,
                    Summary = result.BatchSummary,
                    ActionsHeader = AppLocalizationService.T("Caption.RollingSummary.NewActions"),
                    Actions = FormatActions(result.NewActions),
                    DatesHeader = AppLocalizationService.T("Caption.RollingSummary.DatesAndDeadlines"),
                    Dates = FormatList(result.DatesAndDeadlines.Concat(result.Locations).ToList()),
                    WarningsHeader = AppLocalizationService.T("Caption.RollingSummary.Warnings"),
                    Warnings = FormatList(result.Warnings),
                    UnresolvedHeader = AppLocalizationService.T("Caption.RollingSummary.UnresolvedQuestions"),
                    Unresolved = FormatList(result.UnresolvedQuestions)
                };
            }

            private static string FormatList(IReadOnlyList<string> values)
            {
                return values.Count == 0
                    ? AppLocalizationService.T("ClearBridge.EmptyList")
                    : string.Join(Environment.NewLine, values.Select(value => "- " + value));
            }

            private static string FormatActions(IReadOnlyList<ActionItem> actions)
            {
                if (actions.Count == 0)
                    return AppLocalizationService.T("ClearBridge.EmptyList");

                return string.Join(
                    Environment.NewLine,
                    actions.Select(action =>
                    {
                        var details = new List<string>();
                        if (!string.IsNullOrWhiteSpace(action.Deadline))
                            details.Add(AppLocalizationService.T("ClearBridge.Deadline") + ": " + action.Deadline);
                        if (!string.IsNullOrWhiteSpace(action.Location))
                            details.Add(AppLocalizationService.T("ClearBridge.Location") + ": " + action.Location);
                        return details.Count == 0
                            ? "- " + action.Task
                            : "- " + action.Task + " (" + string.Join("; ", details) + ")";
                    }));
            }
        }
    }
}

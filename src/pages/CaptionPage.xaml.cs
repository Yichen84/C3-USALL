using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.ClearBridge;
using LiveCaptionsTranslator.services.Localization;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator
{
    public partial class CaptionPage : Page
    {
        public const int CARD_HEIGHT = 110;

        private static CaptionPage instance;
        public static CaptionPage Instance => instance;
        private readonly RollingSummarySessionService rollingSummaryService = new();
        private readonly DispatcherTimer rollingSummaryTimer = new();
        private CancellationTokenSource? rollingSummaryCancellation;
        private RollingSummaryOutcome? currentRollingOutcome;
        private bool rollingSummarySaved;

        public CaptionPage()
        {
            InitializeComponent();
            DataContext = Translator.Caption;
            instance = this;
            ApplyLocalization();
            InitializeRollingSummaryControls();

            Loaded += (s, e) =>
            {
                AutoHeight();
                (App.Current.MainWindow as MainWindow).CaptionLogButton.Visibility = Visibility.Visible;
                Translator.Caption.PropertyChanged += TranslatedChanged;
            };
            Unloaded += (s, e) =>
            {
                (App.Current.MainWindow as MainWindow).CaptionLogButton.Visibility = Visibility.Collapsed;
                Translator.Caption.PropertyChanged -= TranslatedChanged;
                StopRollingTimer();
            };

            CollapseTranslatedCaption(Translator.Setting.MainWindow.CaptionLogEnabled);
            rollingSummaryTimer.Tick += RollingSummaryTimer_Tick;
            RenderRollingSummaryState();
        }

        private void ApplyLocalization()
        {
            CaptionRoot.FlowDirection = AppLocalizationService.CurrentFlowDirection;
            AnalyzeCaptionsButton.Content = AppLocalizationService.T("Caption.AnalyzeCaptions");
            AnalyzeCaptionsButton.ToolTip = AppLocalizationService.T("Caption.AnalyzeCaptions.ToolTip");
            OriginalCaption.ToolTip = AppLocalizationService.T("Caption.ClickToCopy");
            TranslatedCaption.ToolTip = AppLocalizationService.T("Caption.ClickToCopy");
            RollingSummaryHeaderText.Text = AppLocalizationService.T("Caption.RollingSummary");
            RollingSummaryPrivacyText.Text = AppLocalizationService.T("Caption.RollingSummary.Privacy");
            RollingSummaryProviderLabel.Text = AppLocalizationService.T("ClearBridge.Provider");
            RollingSummaryOutputLanguageLabel.Text = AppLocalizationService.T("ClearBridge.OutputLanguage");
            RollingSummaryIntervalLabel.Text = AppLocalizationService.T("Caption.RollingSummary.Interval");
            RollingStartButton.Content = AppLocalizationService.T("Caption.RollingSummary.Start");
            RollingPauseButton.Content = AppLocalizationService.T("Caption.RollingSummary.Pause");
            RollingResumeButton.Content = AppLocalizationService.T("Caption.RollingSummary.Resume");
            RollingStopButton.Content = AppLocalizationService.T("Caption.RollingSummary.Stop");
            RollingProcessNowButton.Content = AppLocalizationService.T("Caption.RollingSummary.ProcessNow");
            RollingSaveButton.Content = AppLocalizationService.T("Caption.RollingSummary.SaveConfirmedSummary");
            RollingClearButton.Content = AppLocalizationService.T("Caption.RollingSummary.ClearTemporaryContext");
            RollingKeyPointsHeaderText.Text = AppLocalizationService.T("Caption.RollingSummary.KeyPoints");
            RollingActionsHeaderText.Text = AppLocalizationService.T("Caption.RollingSummary.NewActions");
            RollingDatesHeaderText.Text = AppLocalizationService.T("Caption.RollingSummary.DatesAndDeadlines");
            RollingWarningsHeaderText.Text = AppLocalizationService.T("Caption.RollingSummary.Warnings");
            RollingUnresolvedHeaderText.Text = AppLocalizationService.T("Caption.RollingSummary.UnresolvedQuestions");
            RenderRollingSummaryState();
        }

        private void InitializeRollingSummaryControls()
        {
            RollingSummaryProviderBox.ItemsSource = new[] { "Mock", "OpenAI-compatible" };
            RollingSummaryProviderBox.SelectedItem = "Mock";
            RollingSummaryOutputLanguageBox.ItemsSource = ClearBridgeOutputLanguages.Supported;
            RollingSummaryOutputLanguageBox.SelectedItem = ClearBridgeOutputLanguages.English;
            RollingSummaryIntervalBox.ItemsSource = new[] { 60, 90, 120 };
            RollingSummaryIntervalBox.SelectedItem = RollingSummarySessionService.DefaultIntervalSeconds;
        }

        private async void AnalyzeCaptionsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sentences = Translator.Caption.GetAnalysisSentencesSnapshot();
                if (App.Current.MainWindow is MainWindow mainWindow)
                    await mainWindow.OpenClearBridgeCaptionAnalysisAsync(sentences);
            }
            catch (Exception ex)
            {
                SnackbarHost.Show(
                    AppLocalizationService.T("ClearBridge.Failed"),
                    ex.Message,
                    SnackbarType.Error,
                    timeout: 3,
                    closeButton: true);
            }
        }

        private void RollingStartButton_Click(object sender, RoutedEventArgs e)
        {
            rollingSummaryService.Start();
            rollingSummarySaved = false;
            StartRollingTimer();
            RenderRollingSummaryState(AppLocalizationService.T("Caption.RollingSummary.CollectingCaptions"));
            AutoHeight();
        }

        private void RollingPauseButton_Click(object sender, RoutedEventArgs e)
        {
            rollingSummaryService.Pause();
            StopRollingTimer();
            RenderRollingSummaryState(AppLocalizationService.T("Caption.RollingSummary.PausedCollecting"));
        }

        private void RollingResumeButton_Click(object sender, RoutedEventArgs e)
        {
            rollingSummaryService.Resume();
            StartRollingTimer();
            RenderRollingSummaryState(AppLocalizationService.T("Caption.RollingSummary.CollectingCaptions"));
        }

        private void RollingStopButton_Click(object sender, RoutedEventArgs e)
        {
            StopRollingTimer();
            rollingSummaryService.Stop();
            rollingSummaryCancellation?.Cancel();
            RenderRollingSummaryState(AppLocalizationService.T("Caption.RollingSummary.StoppedDetail"));
        }

        private async void RollingProcessNowButton_Click(object sender, RoutedEventArgs e)
        {
            await ProcessRollingSummaryBatchAsync(isManual: true);
        }

        private async void RollingSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentRollingOutcome == null || rollingSummarySaved)
                return;

            try
            {
                await SQLiteHistoryLogger.LogRollingSummary(
                    sourceDescription: AppLocalizationService.T("Caption.RollingSummary.HistorySource"),
                    resultJson: rollingSummaryService.BuildConfirmedHistoryJson(currentRollingOutcome),
                    summary: currentRollingOutcome.Result.BatchSummary,
                    outputLanguage: GetRollingOutputLanguage(),
                    providerName: currentRollingOutcome.ProviderName,
                    isMock: currentRollingOutcome.IsMock,
                    sessionId: rollingSummaryService.SessionId,
                    sessionStart: rollingSummaryService.SessionStart,
                    sessionEnd: DateTimeOffset.Now,
                    batchCount: rollingSummaryService.ContextCache.BatchCount,
                    confirmedActionCount: currentRollingOutcome.Result.NewActions.Count,
                    userConfirmed: true,
                    token: CancellationToken.None);

                rollingSummarySaved = true;
                RenderRollingSummaryState(AppLocalizationService.T("ClearBridge.History.Success"));
            }
            catch
            {
                RenderRollingSummaryState(AppLocalizationService.T("ClearBridge.History.Failed"));
            }
        }

        private void RollingClearButton_Click(object sender, RoutedEventArgs e)
        {
            rollingSummaryCancellation?.Cancel();
            StopRollingTimer();
            rollingSummaryService.ClearTemporaryContext();
            currentRollingOutcome = null;
            rollingSummarySaved = false;
            RenderRollingSummaryState(AppLocalizationService.T("Caption.RollingSummary.TemporaryContextCleared"));
            AutoHeight();
        }

        public void ClearRollingSummaryForShutdown()
        {
            rollingSummaryCancellation?.Cancel();
            StopRollingTimer();
            rollingSummaryService.ClearTemporaryContext();
            currentRollingOutcome = null;
            rollingSummarySaved = false;
        }

        private async void RollingSummaryTimer_Tick(object? sender, EventArgs e)
        {
            await ProcessRollingSummaryBatchAsync(isManual: false);
        }

        private async Task ProcessRollingSummaryBatchAsync(bool isManual)
        {
            if (!rollingSummaryService.IsRunning || rollingSummaryService.IsPaused || rollingSummaryService.IsProcessing)
            {
                RenderRollingSummaryState();
                return;
            }

            rollingSummaryCancellation = new CancellationTokenSource();
            RenderRollingSummaryState(AppLocalizationService.T("Caption.RollingSummary.ProcessingBatch"));

            try
            {
                var sentences = Translator.Caption.GetAnalysisSentencesSnapshot();
                var outcome = await rollingSummaryService.ProcessPendingAsync(
                    sentences,
                    GetRollingProviderName(),
                    GetRollingOutputLanguage(),
                    rollingSummaryCancellation.Token);

                currentRollingOutcome = outcome;
                rollingSummarySaved = false;
                RenderRollingSummaryOutcome(outcome);
            }
            catch (OperationCanceledException)
            {
                RenderRollingSummaryState(AppLocalizationService.T("ClearBridge.Error.Cancelled"));
            }
            catch (ClearBridgeAnalysisException ex) when (ex.ErrorCode == "WaitingForContent")
            {
                RenderRollingSummaryState(AppLocalizationService.T("Caption.RollingSummary.WaitingForMoreCaptions"));
            }
            catch (ClearBridgeAnalysisException ex)
            {
                var key = "ClearBridge.Error." + ex.ErrorCode;
                var message = AppLocalizationService.T(key);
                RenderRollingSummaryState(message == key ? ex.Message : message);
            }
            catch (Exception ex)
            {
                RenderRollingSummaryState(ex.Message);
            }
            finally
            {
                rollingSummaryCancellation?.Dispose();
                rollingSummaryCancellation = null;
                if (!isManual && rollingSummaryService.IsRunning && !rollingSummaryService.IsPaused)
                    StartRollingTimer();
            }
        }

        private void StartRollingTimer()
        {
            rollingSummaryTimer.Stop();
            rollingSummaryTimer.Interval = TimeSpan.FromSeconds(GetRollingIntervalSeconds());
            if (rollingSummaryService.IsRunning && !rollingSummaryService.IsPaused)
                rollingSummaryTimer.Start();
        }

        private void StopRollingTimer()
        {
            rollingSummaryTimer.Stop();
        }

        private int GetRollingIntervalSeconds()
        {
            return RollingSummaryIntervalBox.SelectedItem is int seconds
                ? rollingSummaryService.NormalizeIntervalSeconds(seconds)
                : RollingSummarySessionService.DefaultIntervalSeconds;
        }

        private string GetRollingProviderName()
        {
            return RollingSummaryProviderBox.SelectedItem as string ?? "Mock";
        }

        private string GetRollingOutputLanguage()
        {
            return RollingSummaryOutputLanguageBox.SelectedItem as string ?? ClearBridgeOutputLanguages.English;
        }

        private void RenderRollingSummaryOutcome(RollingSummaryOutcome outcome)
        {
            var result = outcome.Result;
            RollingTopicText.Text = string.Format(
                "{0}: {1}",
                AppLocalizationService.T("Caption.RollingSummary.CurrentTopic"),
                result.CurrentTopic);
            RollingSummaryText.Text = result.BatchSummary;
            RollingMetaText.Text = string.Format(
                AppLocalizationService.T("Caption.RollingSummary.Meta"),
                outcome.Request.BatchNumber,
                outcome.Request.RangeStart,
                outcome.Request.RangeEnd,
                outcome.Request.ProcessedSentenceCount,
                DateTime.Now.ToString("HH:mm:ss"),
                outcome.IsMock ? AppLocalizationService.T("ClearBridge.MockMode") : outcome.ProviderName);
            RollingKeyPointsText.Text = FormatBulletList(result.KeyPoints);
            RollingActionsText.Text = FormatActions(result.NewActions);
            RollingDatesText.Text = FormatBulletList(result.DatesAndDeadlines.Concat(result.Locations).ToList());
            RollingWarningsText.Text = FormatBulletList(result.Warnings);
            RollingUnresolvedText.Text = FormatBulletList(result.UnresolvedQuestions);
            RenderRollingSummaryState();
            RollingSummaryStatusText.Text = AppLocalizationService.T("Caption.RollingSummary.ReviewBeforeSaving");
        }

        private void RenderRollingSummaryState(string detail = "")
        {
            var status = rollingSummaryService.Status;
            RollingSummaryStatusText.Text = AppLocalizationService.T("Caption.RollingSummary.Status." + status);
            if (!string.IsNullOrWhiteSpace(detail))
                RollingMetaText.Text = detail;

            var isProcessing = rollingSummaryService.IsProcessing;
            var isRunning = rollingSummaryService.IsRunning;
            RollingStartButton.IsEnabled = !isProcessing && !isRunning;
            RollingPauseButton.IsEnabled = !isProcessing && isRunning && !rollingSummaryService.IsPaused;
            RollingResumeButton.IsEnabled = !isProcessing && rollingSummaryService.IsPaused;
            RollingStopButton.IsEnabled = !isProcessing && isRunning;
            RollingProcessNowButton.IsEnabled = !isProcessing && isRunning && !rollingSummaryService.IsPaused;
            RollingSaveButton.IsEnabled = !isProcessing && currentRollingOutcome != null && !rollingSummarySaved;
            RollingClearButton.IsEnabled = !isProcessing;
            RollingSummaryProviderBox.IsEnabled = !isProcessing;
            RollingSummaryOutputLanguageBox.IsEnabled = !isProcessing;
            RollingSummaryIntervalBox.IsEnabled = !isProcessing;

            if (currentRollingOutcome == null)
            {
                RollingTopicText.Text = AppLocalizationService.T("Caption.RollingSummary.NoSummaryYet");
                RollingSummaryText.Text = string.Empty;
                RollingKeyPointsText.Text = FormatBulletList([]);
                RollingActionsText.Text = FormatBulletList([]);
                RollingDatesText.Text = FormatBulletList([]);
                RollingWarningsText.Text = FormatBulletList([]);
                RollingUnresolvedText.Text = FormatBulletList([]);
            }
        }

        private string FormatBulletList(IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return AppLocalizationService.T("ClearBridge.EmptyList");

            return string.Join(Environment.NewLine, values.Select(value => "- " + value));
        }

        private string FormatActions(IReadOnlyList<ActionItem> actions)
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

        private async void TextBlock_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                try
                {
                    Clipboard.SetText(textBlock.Text);
                    SnackbarHost.Show(AppLocalizationService.T("Common.Copied"), textBlock.Text, SnackbarType.Info, 100);
                }
                catch
                {
                    SnackbarHost.Show(AppLocalizationService.T("Common.CopyFailed"), string.Empty, SnackbarType.Error, 100);
                }
                await Task.Delay(500);
            }
        }

        private void TranslatedChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Translator.Caption.DisplayTranslatedCaption))
            {
                if (Encoding.UTF8.GetByteCount(Translator.Caption.DisplayTranslatedCaption) >= TextUtil.LONG_THRESHOLD)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.TranslatedCaption.FontSize = 15;
                    }), DispatcherPriority.Background);
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.TranslatedCaption.FontSize = 18;
                    }), DispatcherPriority.Background);
                }
            }
        }

        public void CollapseTranslatedCaption(bool isCollapsed)
        {
            var converter = new GridLengthConverter();

            if (isCollapsed)
            {
                TranslatedCaption_Row.Height = (GridLength)converter.ConvertFromString("Auto");
                LogCards.Visibility = Visibility.Visible;
            }
            else
            {
                TranslatedCaption_Row.Height = (GridLength)converter.ConvertFromString("*");
                LogCards.Visibility = Visibility.Collapsed;
            }
        }

        public void AutoHeight()
        {
            if (rollingSummaryService.IsRunning || currentRollingOutcome != null)
                (App.Current.MainWindow as MainWindow).AutoHeightAdjust(minHeight: 720, maxHeight: 900);
            else if (Translator.Setting.MainWindow.CaptionLogEnabled)
                (App.Current.MainWindow as MainWindow).AutoHeightAdjust(
                    minHeight: CARD_HEIGHT * (Translator.Setting.DisplaySentences + 1),
                    maxHeight: CARD_HEIGHT * (Translator.Setting.DisplaySentences + 1));
            else
                (App.Current.MainWindow as MainWindow).AutoHeightAdjust(
                    minHeight: (int)App.Current.MainWindow.MinHeight,
                    maxHeight: (int)App.Current.MainWindow.MinHeight);
        }
    }
}

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

using LiveCaptionsTranslator.apis;
using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.ClearBridge;
using LiveCaptionsTranslator.services.Localization;
using LiveCaptionsTranslator.services.Ocr;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator
{
    public partial class ClearBridgePage : Page
    {
        public const int MIN_HEIGHT = 720;

        public static ClearBridgePage? Instance { get; private set; }

        private readonly CrisisActionAnalysisService analysisService = new();
        private readonly ClearBridgeLocalizationService localizer = new();
        private readonly WindowsLocalOcrProvider localOcrProvider = new();
        private readonly AiVisionOcrProvider aiOcrProvider = new();
        private readonly ScreenRegionCaptureService captureService = new();
        private readonly OpenAiPlainSummaryService summaryService = new();

        private CrisisActionAnalysisOutcome? currentOutcome;
        private ClearBridgeImageInput? currentOcrImage;
        private ClearBridgeOcrResult? currentOcrResult;
        private ClearBridgeInputType currentInputType = ClearBridgeInputType.Text;
        private CancellationTokenSource? analyzeCancellation;
        private CancellationTokenSource? ocrCancellation;
        private CancellationTokenSource? textActionCancellation;
        private bool historySaved;
        private bool resultCardsUseSingleColumn;
        private MouseWheelEventArgs? forwardedMouseWheelEvent;
        private bool applyingUiLanguage;
        private bool updatingOcrProviderSelection;
        private string currentStateKey = "Ready";
        private string currentStateDetail = string.Empty;

        public ClearBridgePage()
        {
            InitializeComponent();
            Instance = this;
            ApplicationThemeManager.ApplySystemTheme();

            UiLanguageBox.ItemsSource = ClearBridgeLocalizationService.SupportedUiLanguages;
            UiLanguageBox.SelectedValue = AppLocalizationService.SavedLanguage;
            ProviderBox.ItemsSource = new[] { "Mock", "OpenAI-compatible" };
            ProviderBox.SelectedItem = "Mock";
            OutputLanguageBox.ItemsSource = ClearBridgeOutputLanguages.Supported;
            OutputLanguageBox.SelectedItem = ClearBridgeOutputLanguages.English;
            OcrEngineBox.ItemsSource = new[]
            {
                "Auto",
                localOcrProvider.DisplayName,
                aiOcrProvider.DisplayName
            };
            OcrEngineBox.SelectedItem = "Auto";
            OcrTranslationProviderBox.ItemsSource = Translator.Setting?.Configs.Keys;
            OcrTranslationProviderBox.SelectedItem = Translator.Setting?.ApiName;
            RefreshOcrTargetLanguages();
            RegisterMouseWheelForwardingHandlers();

            Unloaded += (s, e) =>
            {
                if (ReferenceEquals(Instance, this))
                    Instance = null;
            };

            Loaded += (s, e) =>
            {
                Instance = this;
                (App.Current.MainWindow as MainWindow)?.AutoHeightAdjust(
                    minHeight: MIN_HEIGHT,
                    maxHeight: MIN_HEIGHT);
            };

            ApplyLocalization();
            ApplyInputModeState();
            UpdateCharacterCount();
            SetState("Ready");
            SetBusyUi(false);
        }

        private void UiLanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (applyingUiLanguage)
                return;

            if (AppLocalizationService.SaveLanguageForNextRestart(UiLanguageBox.SelectedValue as string))
                SetState("Ready", localizer.T("Settings.UiLanguage.RestartRequired.Message"));
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
                OcrReviewPanel,
                TranslationResultPanel,
                SummaryResultPanel,
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

        private void TextModeButton_Click(object sender, RoutedEventArgs e)
        {
            ClearOcrState(clearText: false);
            SetState("Ready");
        }

        private async void CaptureScreenButton_Click(object sender, RoutedEventArgs e)
        {
            await StartScreenOcrCaptureAsync();
        }

        public async Task StartScreenOcrCaptureAsync()
        {
            if (ocrCancellation != null || analyzeCancellation != null || textActionCancellation != null)
                return;

            var owner = Window.GetWindow(this);
            try
            {
                SetState("OcrSelectArea");
                owner?.Hide();
                await Task.Delay(200);

                var input = captureService.CaptureRegion(CancellationToken.None);
                owner?.Show();
                owner?.Activate();

                await LoadOcrImageAsync(input, ClearBridgeInputType.ScreenRegion, useAiOcr: false);
            }
            catch (OperationCanceledException)
            {
                owner?.Show();
                owner?.Activate();
                SetState("Cancelled", localizer.T("ClearBridge.Ocr.CaptureCancelled"));
            }
            catch (ClearBridgeOcrException ex)
            {
                owner?.Show();
                owner?.Activate();
                SetState("Failed", LocalizeOcrError(ex.ErrorCode));
            }
            catch (Exception)
            {
                owner?.Show();
                owner?.Activate();
                SetState("Failed", localizer.T("ClearBridge.Ocr.Error.CaptureFailed"));
            }
        }

        private async void UploadImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ocrCancellation != null || analyzeCancellation != null || textActionCancellation != null)
                return;

            var dialog = new OpenFileDialog
            {
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|PNG|*.png|JPEG|*.jpg;*.jpeg|BMP|*.bmp",
                Title = localizer.T("ClearBridge.Ocr.UploadImage")
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                SetState("OcrLoadingImage");
                var input = OcrImageUtility.FromFile(dialog.FileName);
                await LoadOcrImageAsync(input, ClearBridgeInputType.ImageFile, useAiOcr: false);
            }
            catch (ClearBridgeOcrException ex)
            {
                SetState("Failed", LocalizeOcrError(ex.ErrorCode));
            }
            catch (Exception)
            {
                SetState("Failed", localizer.T("ClearBridge.Ocr.Error.InvalidImage"));
            }
        }

        private async void RetryOcrButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentOcrImage == null)
                return;

            await RunOcrAsync(ShouldUseAiOcrFromSelection(), requireCloudConfirmation: true);
        }

        private async void RetryAiOcrButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentOcrImage == null)
                return;

            await RunOcrAsync(useAiOcr: true, requireCloudConfirmation: true);
        }

        private void ClearOcrButton_Click(object sender, RoutedEventArgs e)
        {
            ClearOcrState(clearText: true);
            SetState("Ready");
        }

        private void CancelOcrButton_Click(object sender, RoutedEventArgs e)
        {
            var hadActiveOperation =
                ocrCancellation != null ||
                textActionCancellation != null ||
                analyzeCancellation != null;

            ocrCancellation?.Cancel();
            textActionCancellation?.Cancel();
            analyzeCancellation?.Cancel();

            if (!hadActiveOperation)
                ClearOcrState(clearText: false);

            SetState("Cancelled", localizer.T("ClearBridge.Error.Cancelled"));
        }

        private async void OcrTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            await TranslateConfirmedOcrTextAsync();
        }

        private async void OcrSummarizeButton_Click(object sender, RoutedEventArgs e)
        {
            await SummarizeConfirmedOcrTextAsync();
        }

        private async void OcrClearBridgeAnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentOcrResult == null)
                return;

            await AnalyzeAsync(currentInputType, currentOcrResult);
        }

        private void OcrTranslationProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingOcrProviderSelection || OcrTranslationProviderBox.SelectedItem == null || Translator.Setting == null)
                return;

            Translator.Setting.ApiName = OcrTranslationProviderBox.SelectedItem.ToString();
            RefreshOcrTargetLanguages();
        }

        private void ExampleButton_Click(object sender, RoutedEventArgs e)
        {
            ClearOcrState(clearText: false);
            SourceTextBox.Text = MockCrisisActionAnalysisProvider.SampleNotice;
            ProviderBox.SelectedItem = "Mock";
            currentInputType = ClearBridgeInputType.Text;
            ApplyInputModeState();
            SetState("Ready");
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (analyzeCancellation != null)
                return;

            SourceTextBox.Clear();
            currentOutcome = null;
            historySaved = false;
            ClearOcrState(clearText: false);
            HideResultPanels();
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

            await AnalyzeAsync(
                currentOcrResult == null ? ClearBridgeInputType.Text : currentInputType,
                currentOcrResult);
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

            await SaveHistoryAsync(
                currentOutcome,
                SourceTextBox.Text,
                GetSelectedOutputLanguage(),
                showSuccess: true,
                currentInputType,
                currentOcrResult);
        }

        private async void AnalyzeAgainButton_Click(object sender, RoutedEventArgs e)
        {
            await AnalyzeAsync(currentInputType, currentOcrResult);
        }

        private async Task LoadOcrImageAsync(
            ClearBridgeImageInput input,
            ClearBridgeInputType inputType,
            bool useAiOcr)
        {
            currentOcrImage = input;
            currentInputType = inputType;
            OcrPreviewImage.Source = input.ToPreviewImage();
            OcrReviewPanel.Visibility = Visibility.Visible;
            ApplyInputModeState();
            HideResultPanels();
            await RunOcrAsync(useAiOcr, requireCloudConfirmation: useAiOcr);
        }

        private async Task RunOcrAsync(bool useAiOcr, bool requireCloudConfirmation)
        {
            if (currentOcrImage == null)
                return;

            if (useAiOcr && requireCloudConfirmation && !ConfirmAiOcrUpload())
            {
                SetState("Cancelled", localizer.T("ClearBridge.Ocr.AiUploadCancelled"));
                return;
            }

            ocrCancellation = new CancellationTokenSource();
            SetBusyUi(true);
            SetState(useAiOcr ? "OcrExtractingAi" : "OcrExtractingLocal");

            try
            {
                var provider = useAiOcr ? (IClearBridgeOcrProvider)aiOcrProvider : localOcrProvider;
                var result = await provider.ExtractTextAsync(currentOcrImage, ocrCancellation.Token);
                currentOcrResult = result;
                SourceTextBox.Text = result.Text;
                ApplyInputModeState();
                UpdateOcrMetadata();

                if (string.IsNullOrWhiteSpace(result.Text))
                    SetState("OcrNoTextFound", localizer.T("ClearBridge.Ocr.ReviewNextAction"));
                else if (result.Text.Trim().Length < CrisisActionAnalysisService.MinInputLength)
                    SetState("OcrCompleted", localizer.T("ClearBridge.Ocr.TextMayBeShort"));
                else
                    SetState("OcrCompleted", localizer.T("ClearBridge.Ocr.ReviewNextAction"));
            }
            catch (OperationCanceledException)
            {
                SetState("Cancelled", localizer.T("ClearBridge.Error.Cancelled"));
            }
            catch (ClearBridgeOcrException ex)
            {
                SetState("OcrFailed", LocalizeOcrError(ex.ErrorCode));
            }
            catch (Exception)
            {
                SetState("OcrFailed", localizer.T("ClearBridge.Ocr.Error.Unexpected"));
            }
            finally
            {
                ocrCancellation?.Dispose();
                ocrCancellation = null;
                SetBusyUi(false);
            }
        }

        private async Task TranslateConfirmedOcrTextAsync()
        {
            if (currentOcrResult == null)
                return;

            var confirmedText = SourceTextBox.Text;
            if (!ValidateConfirmedOcrText(confirmedText))
                return;

            textActionCancellation = new CancellationTokenSource();
            SetBusyUi(true);
            HideResultPanels();
            SetState("OcrTranslating");

            var previousContextAware = Translator.Setting.ContextAware;
            try
            {
                var provider = OcrTranslationProviderBox.SelectedItem?.ToString() ?? Translator.Setting.ApiName;
                var targetLanguage = OcrTargetLanguageBox.SelectedItem?.ToString() ?? Translator.Setting.TargetLanguage;
                Translator.Setting.ApiName = provider;
                Translator.Setting.TargetLanguage = targetLanguage;
                Translator.Setting.ContextAware = false;

                var translatedText = await TranslateAPI.TranslateFunction(confirmedText, textActionCancellation.Token);
                RenderTranslationResult(confirmedText, translatedText, targetLanguage, provider);
                await SQLiteHistoryLogger.LogOcrTranslation(
                    confirmedText,
                    translatedText,
                    targetLanguage,
                    provider,
                    currentInputType,
                    currentOcrResult.EngineName,
                    currentOcrResult.IsCloudBased,
                    IsOcrTextEdited(),
                    textActionCancellation.Token);
                SetState("Completed");
            }
            catch (OperationCanceledException)
            {
                SetState("Cancelled", localizer.T("ClearBridge.Error.Cancelled"));
            }
            catch (Exception ex)
            {
                SetState("Failed", ex.Message);
            }
            finally
            {
                Translator.Setting.ContextAware = previousContextAware;
                textActionCancellation?.Dispose();
                textActionCancellation = null;
                SetBusyUi(false);
            }
        }

        private async Task SummarizeConfirmedOcrTextAsync()
        {
            if (currentOcrResult == null)
                return;

            var confirmedText = SourceTextBox.Text;
            if (!ValidateConfirmedOcrText(confirmedText))
                return;

            textActionCancellation = new CancellationTokenSource();
            SetBusyUi(true);
            HideResultPanels();
            SetState("OcrSummarizing");

            try
            {
                var summary = await summaryService.SummarizeAsync(
                    confirmedText,
                    GetSelectedOutputLanguage(),
                    textActionCancellation.Token);
                RenderSummaryResult(confirmedText, summary, summaryService.ProviderName);
                await SQLiteHistoryLogger.LogOcrSummary(
                    confirmedText,
                    summary,
                    summaryService.ProviderName,
                    currentInputType,
                    currentOcrResult.EngineName,
                    currentOcrResult.IsCloudBased,
                    IsOcrTextEdited(),
                    textActionCancellation.Token);
                SetState("Completed");
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
                textActionCancellation?.Dispose();
                textActionCancellation = null;
                SetBusyUi(false);
            }
        }

        private async Task AnalyzeAsync(
            ClearBridgeInputType inputType,
            ClearBridgeOcrResult? ocrResult)
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

                if (ocrResult != null)
                {
                    await SaveHistoryAsync(
                        outcome,
                        sourceText,
                        outputLanguage,
                        showSuccess: false,
                        inputType,
                        ocrResult);
                }
                else
                {
                    await SaveHistoryAsync(outcome, sourceText, outputLanguage, showSuccess: false);
                }
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
            bool showSuccess,
            ClearBridgeInputType inputType = ClearBridgeInputType.Text,
            ClearBridgeOcrResult? ocrResult = null)
        {
            try
            {
                await SQLiteHistoryLogger.LogClearBridgeAnalysis(
                    sourceText,
                    outcome,
                    outputLanguage,
                    inputType: inputType,
                    ocrEngine: ocrResult?.EngineName ?? string.Empty,
                    ocrWasCloudBased: ocrResult?.IsCloudBased,
                    ocrTextEdited: ocrResult != null && IsOcrTextEdited(),
                    featureType: ocrResult == null
                        ? SQLiteHistoryLogger.FeatureTypeClearBridge
                        : SQLiteHistoryLogger.FeatureTypeClearBridgeOcr);

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

        private void RenderTranslationResult(
            string originalText,
            string translatedText,
            string targetLanguage,
            string provider)
        {
            HideResultPanels();
            TranslationResultPanel.Visibility = Visibility.Visible;
            TranslationResultHeaderText.Text = localizer.T("ClearBridge.Ocr.TranslateResult");
            TranslationResultMetadataText.Text = string.Join(" | ",
                $"{localizer.T("ClearBridge.Ocr.SourceLanguage")}: OCR",
                $"{localizer.T("ClearBridge.Ocr.TargetLanguage")}: {targetLanguage}",
                $"{localizer.T("ClearBridge.Provider")}: {provider}");
            TranslationOriginalHeaderText.Text = localizer.T("ClearBridge.Ocr.OriginalText");
            TranslationOriginalText.Text = originalText;
            TranslationOutputHeaderText.Text = localizer.T("ClearBridge.Ocr.Translation");
            TranslationOutputText.Text = translatedText;
        }

        private void RenderSummaryResult(
            string originalText,
            string summary,
            string provider)
        {
            HideResultPanels();
            SummaryResultPanel.Visibility = Visibility.Visible;
            SummaryResultHeaderText.Text = localizer.T("ClearBridge.Ocr.SummaryResult");
            SummaryResultMetadataText.Text = $"{localizer.T("ClearBridge.Provider")}: {provider}";
            SummaryOriginalHeaderText.Text = localizer.T("ClearBridge.Ocr.OriginalText");
            SummaryOriginalText.Text = originalText;
            SummaryOutputHeaderText.Text = localizer.T("ClearBridge.Ocr.Summary");
            SummaryOutputText.Text = summary;
        }

        private void HideResultPanels()
        {
            TranslationResultPanel.Visibility = Visibility.Collapsed;
            SummaryResultPanel.Visibility = Visibility.Collapsed;
            ResultPanel.Visibility = Visibility.Collapsed;
        }

        private bool ValidateConfirmedOcrText(string confirmedText)
        {
            if (string.IsNullOrWhiteSpace(confirmedText))
            {
                SetState("Failed", localizer.T("ClearBridge.Error.InputEmpty"));
                return false;
            }

            if (confirmedText.Trim().Length < CrisisActionAnalysisService.MinInputLength)
            {
                SetState("Failed", localizer.T("ClearBridge.Error.InputTooShort"));
                return false;
            }

            return true;
        }

        private bool ConfirmAiOcrUpload()
        {
            var result = MessageBox.Show(
                localizer.T("ClearBridge.Ocr.AiUploadPrompt"),
                localizer.T("ClearBridge.Ocr.AiUploadTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }

        private bool ShouldUseAiOcrFromSelection()
        {
            return string.Equals(
                OcrEngineBox.SelectedItem as string,
                aiOcrProvider.DisplayName,
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsOcrTextEdited()
        {
            if (currentOcrResult == null)
                return false;

            return !string.Equals(
                currentOcrResult.Text?.Trim(),
                SourceTextBox.Text.Trim(),
                StringComparison.Ordinal);
        }

        private void UpdateOcrMetadata()
        {
            if (currentOcrResult == null)
            {
                OcrMetadataText.Text = string.Empty;
                return;
            }

            OcrMetadataText.Text = string.Join(" | ",
                $"{localizer.T("ClearBridge.Ocr.Source")}: {currentOcrImage?.SourceName ?? string.Empty}",
                $"{localizer.T("ClearBridge.Ocr.Engine")}: {currentOcrResult.EngineName}",
                $"{localizer.T("ClearBridge.Ocr.Latency")}: {currentOcrResult.Duration.TotalMilliseconds:N0} ms",
                $"{localizer.T("ClearBridge.Ocr.ImageSize")}: {currentOcrResult.ImageWidth} x {currentOcrResult.ImageHeight}",
                currentOcrResult.IsCloudBased
                    ? localizer.T("ClearBridge.Ocr.CloudProcessing")
                    : localizer.T("ClearBridge.Ocr.LocalProcessing"));
        }

        private void ApplyInputModeState()
        {
            if (TextInputCard == null)
                return;

            var hasOcrInput = currentOcrImage != null || currentOcrResult != null;
            var isTextMode = currentInputType == ClearBridgeInputType.Text && !hasOcrInput;

            TextInputCard.Visibility = isTextMode || hasOcrInput
                ? Visibility.Visible
                : Visibility.Collapsed;
            TextInputActionsPanel.Visibility = isTextMode
                ? Visibility.Visible
                : Visibility.Collapsed;
            OcrReviewPanel.Visibility = hasOcrInput
                ? Visibility.Visible
                : Visibility.Collapsed;

            InputLabel.Text = isTextMode
                ? localizer.T("ClearBridge.Input")
                : localizer.T("ClearBridge.Ocr.ReviewExtractedText");
            SourceTextBox.ToolTip = isTextMode
                ? localizer.T("ClearBridge.Input.Placeholder")
                : localizer.T("ClearBridge.Ocr.ReviewWarning");
        }

        private void ClearOcrState(bool clearText)
        {
            currentOcrImage = null;
            currentOcrResult = null;
            currentInputType = ClearBridgeInputType.Text;
            OcrPreviewImage.Source = null;
            OcrMetadataText.Text = string.Empty;
            OcrReviewPanel.Visibility = Visibility.Collapsed;
            ApplyInputModeState();
            HideResultPanels();

            if (clearText)
                SourceTextBox.Clear();
        }

        private void RenderResult(CrisisActionAnalysisOutcome outcome)
        {
            var result = outcome.Result;
            HideResultPanels();
            ResultPanel.Visibility = Visibility.Visible;

            ResultTitleText.Text = result.Title;
            PriorityText.Text = $"{localizer.T("ClearBridge.Priority")}: {LocalizePriority(result.Priority)}";
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
                $"{localizer.T("ClearBridge.Priority")}: {LocalizePriority(result.Priority)}",
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
            OcrEngineBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            OcrTranslationProviderBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            OcrTargetLanguageBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            UiLanguageBox.SelectedValue = AppLocalizationService.SavedLanguage;
            applyingUiLanguage = false;

            TitleText.Text = localizer.T("ClearBridge.Title");
            SubtitleText.Text = localizer.T("ClearBridge.Subtitle");
            UiLanguageLabel.Text = localizer.T("ClearBridge.UiLanguage");
            ProviderLabel.Text = localizer.T("ClearBridge.Provider");
            OutputLanguageLabel.Text = localizer.T("ClearBridge.OutputLanguage");
            OcrToolsHeaderText.Text = localizer.T("ClearBridge.Ocr.Tools");
            TextModeButton.Content = localizer.T("ClearBridge.Ocr.TextMode");
            CaptureScreenButton.Content = localizer.T("ClearBridge.Ocr.CaptureScreenRegion");
            UploadImageButton.Content = localizer.T("ClearBridge.Ocr.UploadImage");
            OcrEngineLabel.Text = localizer.T("ClearBridge.Ocr.Engine");
            OcrTranslationProviderLabel.Text = localizer.T("ClearBridge.Ocr.TranslationProvider");
            OcrTargetLanguageLabel.Text = localizer.T("ClearBridge.Ocr.TargetLanguage");
            OcrReviewHeaderText.Text = localizer.T("ClearBridge.Ocr.ReviewExtractedText");
            OcrReviewWarningText.Text = localizer.T("ClearBridge.Ocr.ReviewWarning");
            OcrBasicActionsLabel.Text = localizer.T("ClearBridge.Ocr.BasicActions");
            OcrTranslateButton.Content = localizer.T("ClearBridge.Ocr.Translate");
            OcrSummarizeButton.Content = localizer.T("ClearBridge.Ocr.Summarize");
            OcrActionAnalysisLabel.Text = localizer.T("ClearBridge.Ocr.ActionAnalysis");
            OcrClearBridgeAnalyzeButton.Content = localizer.T("ClearBridge.Ocr.ClearBridgeAnalyze");
            RetryOcrButton.Content = localizer.T("ClearBridge.Ocr.RetryOcr");
            RetryAiOcrButton.Content = localizer.T("ClearBridge.Ocr.RetryAiOcr");
            ClearOcrButton.Content = localizer.T("ClearBridge.Ocr.Clear");
            CancelOcrButton.Content = localizer.T("ClearBridge.Cancel");
            ExampleButton.Content = localizer.T("ClearBridge.Example");
            ClearButton.Content = localizer.T("ClearBridge.Clear");
            AnalyzeButton.Content = analyzeCancellation == null
                ? localizer.T("ClearBridge.Ocr.ClearBridgeAnalyze")
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
            ApplyInputModeState();
            UpdateOcrMetadata();
            UpdateCharacterCount();
        }

        private void SetBusyUi(bool isBusy)
        {
            SetAnalyzingUi(isBusy);
        }

        private void SetAnalyzingUi(bool isAnalyzing)
        {
            AnalyzeButton.Content = isAnalyzing
                ? localizer.T("ClearBridge.Cancel")
                : localizer.T("ClearBridge.Ocr.ClearBridgeAnalyze");
            SourceTextBox.IsEnabled = !isAnalyzing;
            ProviderBox.IsEnabled = !isAnalyzing;
            OutputLanguageBox.IsEnabled = !isAnalyzing;
            UiLanguageBox.IsEnabled = !isAnalyzing;
            OcrEngineBox.IsEnabled = !isAnalyzing;
            OcrTranslationProviderBox.IsEnabled = !isAnalyzing;
            OcrTargetLanguageBox.IsEnabled = !isAnalyzing;
            TextModeButton.IsEnabled = !isAnalyzing;
            CaptureScreenButton.IsEnabled = !isAnalyzing;
            UploadImageButton.IsEnabled = !isAnalyzing;
            OcrTranslateButton.IsEnabled = !isAnalyzing && currentOcrResult != null;
            OcrSummarizeButton.IsEnabled = !isAnalyzing && currentOcrResult != null;
            OcrClearBridgeAnalyzeButton.IsEnabled = !isAnalyzing && currentOcrResult != null;
            RetryOcrButton.IsEnabled = !isAnalyzing && currentOcrImage != null;
            RetryAiOcrButton.IsEnabled = !isAnalyzing && currentOcrImage != null;
            ClearOcrButton.IsEnabled = !isAnalyzing && currentOcrImage != null;
            CancelOcrButton.IsEnabled = isAnalyzing || currentOcrImage != null;
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

        private string LocalizeOcrError(string errorCode)
        {
            var key = "ClearBridge.Ocr.Error." + errorCode;
            var value = localizer.T(key);
            return value == key ? localizer.T("ClearBridge.Ocr.Error.Unexpected") : value;
        }

        private string LocalizePriority(string priority)
        {
            var normalized = priority?.Trim().ToLowerInvariant() ?? "medium";
            return normalized is "low" or "medium" or "high" or "urgent"
                ? localizer.T("ClearBridge.Priority." + normalized)
                : localizer.T("ClearBridge.Priority.medium");
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

        private void RefreshOcrTargetLanguages()
        {
            if (Translator.Setting == null || OcrTargetLanguageBox == null)
                return;

            updatingOcrProviderSelection = true;
            try
            {
                if (OcrTranslationProviderBox.SelectedItem is string providerName)
                    Translator.Setting.ApiName = providerName;

                var configType = Translator.Setting[Translator.Setting.ApiName].GetType();
                var languagesProp = configType.GetProperty(
                    "SupportedLanguages",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                while (configType != null && languagesProp == null)
                {
                    configType = configType.BaseType;
                    languagesProp = configType?.GetProperty(
                        "SupportedLanguages",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                }

                languagesProp ??= typeof(TranslateAPIConfig).GetProperty(
                    "SupportedLanguages",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                var supportedLanguages = (Dictionary<string, string>)languagesProp!.GetValue(null)!;
                OcrTargetLanguageBox.ItemsSource = supportedLanguages.Keys;

                var targetLanguage = Translator.Setting.TargetLanguage;
                if (!supportedLanguages.ContainsKey(targetLanguage))
                    supportedLanguages[targetLanguage] = targetLanguage;
                OcrTargetLanguageBox.SelectedItem = targetLanguage;
            }
            finally
            {
                updatingOcrProviderSelection = false;
            }
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

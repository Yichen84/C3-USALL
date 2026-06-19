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
        private bool updatingCaptionRange;
        private string currentStateKey = "Ready";
        private string currentStateDetail = string.Empty;
        private IReadOnlyList<CaptionAnalysisSentence> captionAnalysisSentences = [];
        private CaptionAnalysisRequest? currentCaptionRequest;

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
                analyzeCancellation?.Cancel();
                ocrCancellation?.Cancel();
                textActionCancellation?.Cancel();
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

        private void CaptionScope_Checked(object sender, RoutedEventArgs e)
        {
            if (updatingCaptionRange)
                return;

            UpdateCaptionSelectionPreview();
        }

        private void CaptionRangeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (updatingCaptionRange)
                return;

            UpdateCaptionSelectionPreview();
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
                CaptionAnalysisCard,
                CaptionPreviewText,
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
            ClearCaptionAnalysisState();
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
            ClearCaptionAnalysisState();
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

        private async void CaptionAnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (analyzeCancellation != null)
            {
                analyzeCancellation.Cancel();
                return;
            }

            await AnalyzeCaptionSelectionAsync();
        }

        private void CaptionCancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (analyzeCancellation != null)
            {
                analyzeCancellation.Cancel();
                return;
            }

            ClearCaptionAnalysisState();
            currentInputType = ClearBridgeInputType.Text;
            ApplyInputModeState();
            HideResultPanels();
            SetState("Ready");
        }

        private void CaptionResetRangeButton_Click(object sender, RoutedEventArgs e)
        {
            ResetCaptionRange();
            UpdateCaptionSelectionPreview();
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
            if (currentInputType == ClearBridgeInputType.CaptionAnalysis)
                await AnalyzeCaptionSelectionAsync();
            else
                await AnalyzeAsync(currentInputType, currentOcrResult);
        }

        public void LoadCaptionAnalysis(IReadOnlyList<CaptionAnalysisSentence> sentences)
        {
            captionAnalysisSentences = sentences
                .OrderBy(sentence => sentence.Number)
                .Select(sentence => new CaptionAnalysisSentence
                {
                    Number = sentence.Number,
                    SourceText = sentence.SourceText,
                    TranslatedText = sentence.TranslatedText,
                    Timestamp = sentence.Timestamp
                })
                .ToList();

            currentInputType = ClearBridgeInputType.CaptionAnalysis;
            currentOcrImage = null;
            currentOcrResult = null;
            currentOutcome = null;
            currentCaptionRequest = null;
            historySaved = false;
            SourceTextBox.Clear();
            ResetCaptionRange();
            ApplyInputModeState();
            HideResultPanels();
            ApplyHistoryButtonState();
            UpdateCaptionSelectionPreview();

            var detail = captionAnalysisSentences.Count == 0
                ? localizer.T("ClearBridge.Error.NoCaptionsAvailable")
                : localizer.T("ClearBridge.Caption.CaptionsMayContainRecognitionErrors");
            SetState("Ready", detail);
            PageScrollViewer.ScrollToTop();
        }

        private async Task AnalyzeCaptionSelectionAsync()
        {
            var outputLanguage = GetSelectedOutputLanguage();
            var providerName = GetSelectedProvider();

            analyzeCancellation = new CancellationTokenSource();
            SetAnalyzingUi(true);
            currentCaptionRequest = null;
            currentOutcome = null;
            historySaved = false;
            HideResultPanels();
            ApplyHistoryButtonState();
            SetState("Analyzing", localizer.T("ClearBridge.Caption.ReviewBeforeSaving"));

            try
            {
                var request = CreateCaptionAnalysisRequest();
                var outcome = await analysisService.AnalyzeCaptionAsync(
                    providerName,
                    request.Text,
                    outputLanguage,
                    analyzeCancellation.Token);

                currentCaptionRequest = request;
                currentOutcome = outcome;
                historySaved = false;
                SourceTextBox.Text = request.Text;
                RenderResult(outcome);

                if (outcome.IsMock)
                {
                    SetState("MockMode", outcome.UsedFallback
                        ? localizer.T("ClearBridge.Fallback.Detail")
                        : localizer.T("ClearBridge.MockMode.Detail"));
                }
                else
                {
                    SetState("Completed", localizer.T("ClearBridge.Caption.ReviewBeforeSaving"));
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
                UpdateCaptionSelectionPreview();
            }
        }

        private CaptionAnalysisRequest CreateCaptionAnalysisRequest()
        {
            var analyzeAll = CaptionAllRadio.IsChecked == true;
            var from = ParseCaptionRangeValue(CaptionFromBox.Text, 1);
            var to = ParseCaptionRangeValue(CaptionToBox.Text, captionAnalysisSentences.Count);
            return CaptionAnalysisPreprocessor.Prepare(captionAnalysisSentences, analyzeAll, from, to);
        }

        private void ResetCaptionRange()
        {
            if (CaptionAllRadio == null)
                return;

            updatingCaptionRange = true;
            try
            {
                CaptionAllRadio.IsChecked = true;
                CaptionRangeRadio.IsChecked = false;
                CaptionFromBox.Text = captionAnalysisSentences.Count == 0 ? "0" : "1";
                CaptionToBox.Text = captionAnalysisSentences.Count.ToString();
            }
            finally
            {
                updatingCaptionRange = false;
            }
        }

        private void UpdateCaptionSelectionPreview()
        {
            if (CaptionAnalyzeButton == null)
                return;

            var total = captionAnalysisSentences.Count;
            var analyzeAll = CaptionAllRadio.IsChecked == true;
            var from = analyzeAll ? 1 : ParseCaptionRangeValue(CaptionFromBox.Text, 1);
            var to = analyzeAll ? total : ParseCaptionRangeValue(CaptionToBox.Text, total);
            var selectedCount = total == 0 || from < 1 || to > total || from > to
                ? 0
                : to - from + 1;
            var validRange = total > 0 && from >= 1 && to <= total && from <= to;
            var rangeTooLarge = selectedCount > CaptionAnalysisPreprocessor.MaxSentences;

            CaptionFromBox.IsEnabled = !analyzeAll && analyzeCancellation == null;
            CaptionToBox.IsEnabled = !analyzeAll && analyzeCancellation == null;
            CaptionTotalText.Text = localizer.Format("ClearBridge.Caption.TotalSentences", total);
            CaptionSelectedRangeText.Text = selectedCount == 0
                ? localizer.T("ClearBridge.Caption.InvalidRange")
                : localizer.Format("ClearBridge.Caption.SelectedRange", from, to);
            CaptionSelectedCountText.Text = localizer.Format("ClearBridge.Caption.SelectedSentences", selectedCount);
            CaptionLimitText.Text = rangeTooLarge
                ? localizer.T("ClearBridge.Error.RangeTooLarge")
                : localizer.T("ClearBridge.Caption.Maximum400Sentences");
            CaptionPreviewText.Text = BuildCaptionPreview(from, to, validRange);
            CaptionAnalyzeButton.IsEnabled = analyzeCancellation != null ||
                (validRange && !rangeTooLarge && selectedCount > 0);
        }

        private string BuildCaptionPreview(int from, int to, bool validRange)
        {
            if (captionAnalysisSentences.Count == 0)
                return localizer.T("ClearBridge.Error.NoCaptionsAvailable");

            if (!validRange)
                return localizer.T("ClearBridge.Error.InvalidRange");

            var selected = captionAnalysisSentences
                .Where(sentence => sentence.Number >= from && sentence.Number <= to)
                .ToList();
            var preview = selected.Count <= 6
                ? selected
                : selected.Take(3).Concat(selected.TakeLast(3));
            var lines = preview.Select(sentence => $"[{sentence.Number}] {sentence.SourceText}");
            var suffix = selected.Count > 6
                ? Environment.NewLine + "..."
                : string.Empty;
            var charCount = selected.Sum(sentence => sentence.SourceText.Length);
            return string.Join(Environment.NewLine, lines) +
                   suffix +
                   Environment.NewLine +
                   localizer.Format("ClearBridge.Caption.CharacterEstimate", charCount);
        }

        private static int ParseCaptionRangeValue(string text, int fallback)
        {
            return int.TryParse(text, out var value) ? value : fallback;
        }

        public Task LoadOcrReviewAsync(
            ClearBridgeImageInput input,
            ClearBridgeOcrResult result,
            ClearBridgeInputType inputType,
            string? confirmedText = null)
        {
            ClearCaptionAnalysisState();
            currentOcrImage = input;
            currentOcrResult = result;
            currentInputType = inputType;
            currentOutcome = null;
            historySaved = false;
            OcrPreviewImage.Source = input.ToPreviewImage();
            SourceTextBox.Text = confirmedText ?? result.Text;
            ApplyInputModeState();
            UpdateOcrMetadata();
            HideResultPanels();
            ApplyHistoryButtonState();
            SetState("OcrCompleted", localizer.T("ClearBridge.Ocr.ReviewNextAction"));
            return Task.CompletedTask;
        }

        public async Task LoadOcrTranslationResultAsync(
            ClearBridgeImageInput input,
            ClearBridgeOcrResult result,
            ClearBridgeInputType inputType,
            string confirmedText,
            string translatedText,
            string targetLanguage,
            string provider)
        {
            await LoadOcrReviewAsync(input, result, inputType, confirmedText);
            RenderTranslationResult(confirmedText, translatedText, targetLanguage, provider);
            SetState("Completed");
        }

        public async Task LoadOcrSummaryResultAsync(
            ClearBridgeImageInput input,
            ClearBridgeOcrResult result,
            ClearBridgeInputType inputType,
            string confirmedText,
            string summary,
            string provider)
        {
            await LoadOcrReviewAsync(input, result, inputType, confirmedText);
            RenderSummaryResult(confirmedText, summary, provider);
            SetState("Completed");
        }

        public async Task LoadOcrAnalysisResultAsync(
            ClearBridgeImageInput input,
            ClearBridgeOcrResult result,
            ClearBridgeInputType inputType,
            string confirmedText,
            CrisisActionAnalysisOutcome outcome)
        {
            await LoadOcrReviewAsync(input, result, inputType, confirmedText);
            currentOutcome = outcome;
            historySaved = true;
            RenderResult(outcome);
            SetState(outcome.IsMock ? "MockMode" : "Completed");
        }

        private async Task LoadOcrImageAsync(
            ClearBridgeImageInput input,
            ClearBridgeInputType inputType,
            bool useAiOcr)
        {
            ClearCaptionAnalysisState();
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
                    inputType: currentCaptionRequest == null
                        ? inputType
                        : ClearBridgeInputType.CaptionAnalysis,
                    ocrEngine: ocrResult?.EngineName ?? string.Empty,
                    ocrWasCloudBased: ocrResult?.IsCloudBased,
                    ocrTextEdited: ocrResult != null && IsOcrTextEdited(),
                    featureType: currentCaptionRequest != null
                        ? SQLiteHistoryLogger.FeatureTypeClearBridgeCaptionAnalysis
                        : ocrResult == null
                            ? SQLiteHistoryLogger.FeatureTypeClearBridge
                            : SQLiteHistoryLogger.FeatureTypeClearBridgeOcr,
                    analysisScope: currentCaptionRequest?.AnalysisScope ?? string.Empty,
                    rangeStart: currentCaptionRequest?.RangeStart,
                    rangeEnd: currentCaptionRequest?.RangeEnd,
                    originalSentenceCount: currentCaptionRequest?.OriginalSentenceCount,
                    processedSentenceCount: currentCaptionRequest?.ProcessedSentenceCount,
                    selectedCharacterCount: currentCaptionRequest?.CharacterCount,
                    userConfirmed: currentCaptionRequest == null ? null : true);

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

            var isCaptionMode = currentInputType == ClearBridgeInputType.CaptionAnalysis;
            var hasOcrInput = currentOcrImage != null || currentOcrResult != null;
            var isTextMode = currentInputType == ClearBridgeInputType.Text && !hasOcrInput;

            CaptionAnalysisCard.Visibility = isCaptionMode
                ? Visibility.Visible
                : Visibility.Collapsed;
            OcrToolsCard.Visibility = isCaptionMode
                ? Visibility.Collapsed
                : Visibility.Visible;
            TextInputCard.Visibility = !isCaptionMode && (isTextMode || hasOcrInput)
                ? Visibility.Visible
                : Visibility.Collapsed;
            TextInputActionsPanel.Visibility = isTextMode
                ? Visibility.Visible
                : Visibility.Collapsed;
            OcrReviewPanel.Visibility = !isCaptionMode && hasOcrInput
                ? Visibility.Visible
                : Visibility.Collapsed;

            InputLabel.Text = isTextMode
                ? localizer.T("ClearBridge.Input")
                : localizer.T("ClearBridge.Ocr.ReviewExtractedText");
            SourceTextBox.ToolTip = isTextMode
                ? localizer.T("ClearBridge.Input.Placeholder")
                : localizer.T("ClearBridge.Ocr.ReviewWarning");

            UpdateCaptionSelectionPreview();
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

        private void ClearCaptionAnalysisState()
        {
            captionAnalysisSentences = [];
            currentCaptionRequest = null;
            if (currentInputType == ClearBridgeInputType.CaptionAnalysis)
                currentInputType = ClearBridgeInputType.Text;
        }

        private void RenderResult(CrisisActionAnalysisOutcome outcome)
        {
            var result = outcome.Result;
            HideResultPanels();
            ResultPanel.Visibility = Visibility.Visible;

            ResultTitleText.Text = result.Title;
            PriorityText.Text = $"{localizer.T("ClearBridge.Priority")}: {LocalizePriority(result.Priority)}";
            SummaryHeaderText.Text = localizer.T("ClearBridge.SimpleSummary");
            ResponsibleAiNoticeText.Text = localizer.T("ClearBridge.ResponsibleAiNotice");
            SummaryText.Text = result.Summary;

            ImportantPointsList.ItemsSource = BuildStringRows(result.ImportantPoints);
            WarningsList.ItemsSource = BuildStringRows(result.Warnings);
            UnclearItemsList.ItemsSource = BuildStringRows(result.UnclearItems);
            ActionsList.ItemsSource = result.Actions
                .Select(action => new ActionItemViewModel(action, localizer))
                .ToList();
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
            CaptionFromBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            CaptionToBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
            CaptionPreviewText.FlowDirection = System.Windows.FlowDirection.LeftToRight;
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
            CaptionAnalysisHeaderText.Text = localizer.T("ClearBridge.Caption.AnalyzeCaptions");
            CaptionRecognitionWarningText.Text = localizer.T("ClearBridge.Caption.CaptionsMayContainRecognitionErrors");
            CaptionScopeLabel.Text = localizer.T("ClearBridge.Caption.AnalysisScope");
            CaptionAllRadio.Content = localizer.T("ClearBridge.Caption.AllCaptions");
            CaptionRangeRadio.Content = localizer.T("ClearBridge.Caption.SentenceRange");
            CaptionFromLabel.Text = localizer.T("ClearBridge.Caption.FromSentence");
            CaptionToLabel.Text = localizer.T("ClearBridge.Caption.ToSentence");
            CaptionPreviewHeaderText.Text = localizer.T("ClearBridge.Caption.PreviewSelection");
            CaptionAnalyzeButton.Content = localizer.T("ClearBridge.Caption.AnalyzeSelectedRange");
            CaptionCancelButton.Content = localizer.T("ClearBridge.Cancel");
            CaptionResetRangeButton.Content = localizer.T("ClearBridge.Caption.ResetRange");
            ExampleButton.Content = localizer.T("ClearBridge.Example");
            ClearButton.Content = localizer.T("ClearBridge.Clear");
            AnalyzeButton.Content = analyzeCancellation == null
                ? localizer.T("ClearBridge.Ocr.ClearBridgeAnalyze")
                : localizer.T("ClearBridge.Cancel");
            StatusTitleText.Text = localizer.T("ClearBridge.Status");
            ResponsibleAiNoticeText.Text = localizer.T("ClearBridge.ResponsibleAiNotice");
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
            UpdateCaptionSelectionPreview();
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
            CaptionAllRadio.IsEnabled = !isAnalyzing;
            CaptionRangeRadio.IsEnabled = !isAnalyzing;
            CaptionResetRangeButton.IsEnabled = !isAnalyzing;
            CaptionCancelButton.IsEnabled = true;
            CaptionAnalyzeButton.Content = isAnalyzing
                ? localizer.T("ClearBridge.Cancel")
                : localizer.T("ClearBridge.Caption.AnalyzeSelectedRange");
            ExampleButton.IsEnabled = !isAnalyzing;
            ClearButton.IsEnabled = !isAnalyzing;
            CopySummaryButton.IsEnabled = !isAnalyzing;
            CopyActionPlanButton.IsEnabled = !isAnalyzing;
            SaveHistoryButton.IsEnabled = !isAnalyzing && currentOutcome != null && !historySaved;
            AnalyzeAgainButton.IsEnabled = !isAnalyzing;
            ApplyHistoryButtonState();
            UpdateCaptionSelectionPreview();
        }

        private void ApplyHistoryButtonState()
        {
            if (SaveHistoryButton == null)
                return;

            SaveHistoryButton.Content = historySaved
                ? localizer.T("ClearBridge.SavedToHistory")
                : localizer.T("ClearBridge.SaveToHistory");
            SaveHistoryButton.ToolTip = historySaved
                ? localizer.T(currentCaptionRequest == null
                    ? "ClearBridge.SavedToHistory.Detail"
                    : "ClearBridge.Caption.SavedToHistory.Detail")
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

using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

using LiveCaptionsTranslator.apis;
using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.ClearBridge;
using LiveCaptionsTranslator.services.Localization;
using LiveCaptionsTranslator.services.Ocr;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator
{
    public partial class OcrQuickActionWindow : Window
    {
        private readonly ClearBridgeLocalizationService localizer = new();
        private readonly WindowsLocalOcrProvider localOcrProvider = new();
        private readonly AiVisionOcrProvider aiOcrProvider = new();
        private readonly OpenAiPlainSummaryService summaryService = new();
        private readonly CrisisActionAnalysisService analysisService = new();

        private ClearBridgeImageInput imageInput;
        private ClearBridgeOcrResult ocrResult;
        private CancellationTokenSource? actionCancellation;
        private QuickActionResultMode resultMode = QuickActionResultMode.None;
        private string confirmedText;
        private string? translationResult;
        private string? translationTargetLanguage;
        private string? translationProvider;
        private string? summaryResult;
        private CrisisActionAnalysisOutcome? analysisOutcome;

        public OcrQuickActionWindow(ClearBridgeImageInput imageInput, ClearBridgeOcrResult ocrResult)
        {
            InitializeComponent();
            this.imageInput = imageInput;
            this.ocrResult = ocrResult;
            confirmedText = ocrResult.Text ?? string.Empty;

            Loaded += (s, e) =>
            {
                ApplyLocalization();
                RefreshPreview();
                PositionNearSelection();
            };
        }

        private void ApplyLocalization()
        {
            FlowDirection = AppLocalizationService.CurrentFlowDirection;
            StatusText.FlowDirection = FlowDirection.LeftToRight;
            PreviewText.FlowDirection = FlowDirection.LeftToRight;

            TranslateButton.Content = localizer.T("ClearBridge.Ocr.Translate");
            SummarizeButton.Content = localizer.T("ClearBridge.Ocr.Summarize");
            ClearBridgeButton.Content = localizer.T("ClearBridge.Ocr.ClearBridgeAnalyze");
            OpenFullReviewButton.Content = localizer.T("ClearBridge.Quick.OpenFullReview");
            RetryOcrButton.Content = localizer.T("ClearBridge.Ocr.RetryOcr");
            CloseButton.Content = localizer.T("ClearBridge.Quick.Close");
            CopyButton.Content = localizer.T("ClearBridge.Quick.Copy");
            OpenFullResultButton.Content = localizer.T("ClearBridge.Quick.OpenFullResult");
            BackButton.Content = localizer.T("ClearBridge.Quick.Back");
            ResultCloseButton.Content = localizer.T("ClearBridge.Quick.Close");
        }

        private void RefreshPreview()
        {
            StatusText.Text = string.Join(" · ",
                localizer.T("ClearBridge.Quick.OcrCompleted"),
                ocrResult.EngineName,
                localizer.Format("ClearBridge.Quick.CharacterCount", confirmedText.Length),
                ocrResult.IsCloudBased
                    ? localizer.T("ClearBridge.Quick.Cloud")
                    : localizer.T("ClearBridge.Quick.Local"));

            PreviewText.Text = string.IsNullOrWhiteSpace(confirmedText)
                ? localizer.T("ClearBridge.OcrNoTextFound")
                : BuildPreviewText(confirmedText);

            var needsReview = NeedsReviewBeforeActionAnalysis();
            ReviewHintText.Text = needsReview
                ? localizer.T("ClearBridge.Quick.TextMayNeedReview")
                : string.Empty;
            ReviewHintText.Visibility = needsReview ? Visibility.Visible : Visibility.Collapsed;

            ApplyBusyState(false);
        }

        private static string BuildPreviewText(string text)
        {
            var lines = text
                .Replace("\r", string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(5);

            return string.Join(Environment.NewLine, lines);
        }

        private void PositionNearSelection()
        {
            var width = ActualWidth > 0 ? ActualWidth : Width;
            var height = ActualHeight > 0 ? ActualHeight : 260;
            var bounds = imageInput.SourceScreenBounds;
            if (bounds == null)
            {
                var screen = (Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0]).WorkingArea;
                Left = screen.Left + (screen.Width - width) / 2;
                Top = screen.Top + (screen.Height - height) / 2;
                return;
            }

            var source = PresentationSource.FromVisual(this);
            var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var selection = TransformRect(bounds.Value, transform);
            var screenArea = TransformRect(Forms.Screen.FromRectangle(bounds.Value).WorkingArea, transform);
            const double gap = 12;

            var candidates = new[]
            {
                new Point(selection.Right + gap, selection.Bottom + gap),
                new Point(selection.Left - width - gap, selection.Bottom + gap),
                new Point(selection.Right + gap, selection.Top - height - gap),
                new Point(selection.Left - width - gap, selection.Top - height - gap)
            };

            var chosen = candidates.FirstOrDefault(point =>
                point.X >= screenArea.Left &&
                point.Y >= screenArea.Top &&
                point.X + width <= screenArea.Right &&
                point.Y + height <= screenArea.Bottom);

            if (chosen == default)
            {
                chosen = new Point(
                    Math.Clamp(selection.Right - width, screenArea.Left, screenArea.Right - width),
                    Math.Clamp(selection.Bottom + gap, screenArea.Top, screenArea.Bottom - height));
            }

            Left = chosen.X;
            Top = chosen.Y;
        }

        private static Rect TransformRect(System.Drawing.Rectangle rect, Matrix transform)
        {
            var topLeft = transform.Transform(new Point(rect.Left, rect.Top));
            var bottomRight = transform.Transform(new Point(rect.Right, rect.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        private async void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureTextAvailable())
                return;

            actionCancellation = new CancellationTokenSource();
            ApplyBusyState(true, localizer.T("ClearBridge.OcrTranslating"));
            var setting = Translator.Setting!;
            var previousContextAware = setting.ContextAware;

            try
            {
                translationProvider = setting.ApiName;
                translationTargetLanguage = setting.TargetLanguage;
                setting.ContextAware = false;
                translationResult = await TranslateAPI.TranslateFunction(confirmedText, actionCancellation.Token);

                await SQLiteHistoryLogger.LogOcrTranslation(
                    confirmedText,
                    translationResult,
                    translationTargetLanguage,
                    translationProvider,
                    ClearBridgeInputType.ScreenRegion,
                    ocrResult.EngineName,
                    ocrResult.IsCloudBased,
                    ocrTextEdited: false,
                    actionCancellation.Token);

                ShowResult(
                    QuickActionResultMode.Translation,
                    localizer.T("ClearBridge.Ocr.TranslateResult"),
                    translationResult);
            }
            catch (OperationCanceledException)
            {
                ShowError(localizer.T("ClearBridge.Error.Cancelled"));
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                setting.ContextAware = previousContextAware;
                actionCancellation?.Dispose();
                actionCancellation = null;
                ApplyBusyState(false);
            }
        }

        private async void SummarizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureTextAvailable())
                return;

            actionCancellation = new CancellationTokenSource();
            ApplyBusyState(true, localizer.T("ClearBridge.OcrSummarizing"));

            try
            {
                summaryResult = await summaryService.SummarizeAsync(
                    confirmedText,
                    GetQuickOutputLanguage(),
                    actionCancellation.Token);

                await SQLiteHistoryLogger.LogOcrSummary(
                    confirmedText,
                    summaryResult,
                    summaryService.ProviderName,
                    ClearBridgeInputType.ScreenRegion,
                    ocrResult.EngineName,
                    ocrResult.IsCloudBased,
                    ocrTextEdited: false,
                    actionCancellation.Token);

                ShowResult(
                    QuickActionResultMode.Summary,
                    localizer.T("ClearBridge.Ocr.SummaryResult"),
                    summaryResult);
            }
            catch (OperationCanceledException)
            {
                ShowError(localizer.T("ClearBridge.Error.Cancelled"));
            }
            catch (ClearBridgeAnalysisException ex)
            {
                ShowError(LocalizeClearBridgeError(ex.ErrorCode));
            }
            catch (Exception)
            {
                ShowError(localizer.T("ClearBridge.Error.Unexpected"));
            }
            finally
            {
                actionCancellation?.Dispose();
                actionCancellation = null;
                ApplyBusyState(false);
            }
        }

        private async void ClearBridgeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureTextAvailable())
                return;

            if (NeedsReviewBeforeActionAnalysis())
            {
                var decision = MessageBox.Show(
                    localizer.T("ClearBridge.Quick.ReviewBeforeActionAnalysis"),
                    localizer.T("ClearBridge.Quick.TextMayNeedReview"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (decision == MessageBoxResult.OK)
                    await OpenFullReviewAsync();

                return;
            }

            actionCancellation = new CancellationTokenSource();
            ApplyBusyState(true, localizer.T("ClearBridge.Analyzing"));

            try
            {
                analysisOutcome = await analysisService.AnalyzeAsync(
                    "OpenAI-compatible",
                    confirmedText,
                    GetQuickOutputLanguage(),
                    actionCancellation.Token);

                await SQLiteHistoryLogger.LogClearBridgeAnalysis(
                    confirmedText,
                    analysisOutcome,
                    GetQuickOutputLanguage(),
                    token: actionCancellation.Token,
                    inputType: ClearBridgeInputType.ScreenRegion,
                    ocrEngine: ocrResult.EngineName,
                    ocrWasCloudBased: ocrResult.IsCloudBased,
                    ocrTextEdited: false,
                    featureType: SQLiteHistoryLogger.FeatureTypeClearBridgeOcr);

                ShowResult(
                    QuickActionResultMode.ClearBridge,
                    localizer.T("ClearBridge.Title"),
                    BuildClearBridgePreview(analysisOutcome.Result));
            }
            catch (OperationCanceledException)
            {
                ShowError(localizer.T("ClearBridge.Error.Cancelled"));
            }
            catch (ClearBridgeAnalysisException ex)
            {
                ShowError(LocalizeClearBridgeError(ex.ErrorCode));
            }
            catch (Exception)
            {
                ShowError(localizer.T("ClearBridge.Error.Unexpected"));
            }
            finally
            {
                actionCancellation?.Dispose();
                actionCancellation = null;
                ApplyBusyState(false);
            }
        }

        private async void OpenFullReviewButton_Click(object sender, RoutedEventArgs e)
        {
            await OpenFullReviewAsync();
        }

        private async void RetryOcrButton_Click(object sender, RoutedEventArgs e)
        {
            actionCancellation = new CancellationTokenSource();
            ApplyBusyState(true, ocrResult.IsCloudBased
                ? localizer.T("ClearBridge.OcrExtractingAi")
                : localizer.T("ClearBridge.OcrExtractingLocal"));

            try
            {
                IClearBridgeOcrProvider provider = ocrResult.IsCloudBased ? aiOcrProvider : localOcrProvider;
                if (provider.IsCloudBased && !ConfirmAiOcrUpload())
                    return;

                ocrResult = await provider.ExtractTextAsync(imageInput, actionCancellation.Token);
                confirmedText = ocrResult.Text ?? string.Empty;
                resultMode = QuickActionResultMode.None;
                translationResult = null;
                summaryResult = null;
                analysisOutcome = null;
                ResultPanel.Visibility = Visibility.Collapsed;
                ActionButtonsPanel.Visibility = Visibility.Visible;
                ResultButtonsPanel.Visibility = Visibility.Collapsed;
                RefreshPreview();
            }
            catch (OperationCanceledException)
            {
                ShowError(localizer.T("ClearBridge.Error.Cancelled"));
            }
            catch (ClearBridgeOcrException ex)
            {
                ShowError(LocalizeOcrError(ex.ErrorCode));
            }
            catch (Exception)
            {
                ShowError(localizer.T("ClearBridge.Ocr.Error.Unexpected"));
            }
            finally
            {
                actionCancellation?.Dispose();
                actionCancellation = null;
                ApplyBusyState(false);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            actionCancellation?.Cancel();
            Close();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var text = resultMode switch
            {
                QuickActionResultMode.Translation => translationResult,
                QuickActionResultMode.Summary => summaryResult,
                QuickActionResultMode.ClearBridge => analysisOutcome == null
                    ? null
                    : BuildClearBridgePreview(analysisOutcome.Result),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                Clipboard.SetText(text);
                ReviewHintText.Text = localizer.T("ClearBridge.Copy.Success");
                ReviewHintText.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                ShowError(localizer.T("ClearBridge.Error.Clipboard"));
            }
        }

        private async void OpenFullResultButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current.MainWindow is not MainWindow mainWindow)
                return;

            switch (resultMode)
            {
                case QuickActionResultMode.Translation when
                    translationResult != null &&
                    translationTargetLanguage != null &&
                    translationProvider != null:
                    await mainWindow.OpenClearBridgeOcrTranslationResultAsync(
                        imageInput,
                        ocrResult,
                        ClearBridgeInputType.ScreenRegion,
                        confirmedText,
                        translationResult,
                        translationTargetLanguage,
                        translationProvider);
                    Close();
                    break;

                case QuickActionResultMode.Summary when summaryResult != null:
                    await mainWindow.OpenClearBridgeOcrSummaryResultAsync(
                        imageInput,
                        ocrResult,
                        ClearBridgeInputType.ScreenRegion,
                        confirmedText,
                        summaryResult,
                        summaryService.ProviderName);
                    Close();
                    break;

                case QuickActionResultMode.ClearBridge when analysisOutcome != null:
                    await mainWindow.OpenClearBridgeOcrAnalysisResultAsync(
                        imageInput,
                        ocrResult,
                        ClearBridgeInputType.ScreenRegion,
                        confirmedText,
                        analysisOutcome);
                    Close();
                    break;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            resultMode = QuickActionResultMode.None;
            ResultPanel.Visibility = Visibility.Collapsed;
            ActionButtonsPanel.Visibility = Visibility.Visible;
            ResultButtonsPanel.Visibility = Visibility.Collapsed;
            RefreshPreview();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void CardRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
                return;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            actionCancellation?.Cancel();
            actionCancellation?.Dispose();
            actionCancellation = null;
            base.OnClosed(e);
        }

        private async Task OpenFullReviewAsync()
        {
            if (App.Current.MainWindow is not MainWindow mainWindow)
                return;

            await mainWindow.OpenClearBridgeOcrReviewAsync(
                imageInput,
                ocrResult,
                ClearBridgeInputType.ScreenRegion,
                confirmedText);
            Close();
        }

        private void ShowResult(QuickActionResultMode mode, string title, string text)
        {
            resultMode = mode;
            ResultPanel.Visibility = Visibility.Visible;
            ActionButtonsPanel.Visibility = Visibility.Collapsed;
            ResultButtonsPanel.Visibility = Visibility.Visible;
            ResultTitleText.Text = title;
            ResultText.Text = text;
            ReviewHintText.Visibility = Visibility.Collapsed;
        }

        private void ShowError(string message)
        {
            ReviewHintText.Text = message;
            ReviewHintText.Visibility = Visibility.Visible;
        }

        private void ApplyBusyState(bool isBusy, string? status = null)
        {
            if (!string.IsNullOrWhiteSpace(status))
                StatusText.Text = status;

            var hasText = !string.IsNullOrWhiteSpace(confirmedText);
            TranslateButton.IsEnabled = !isBusy && hasText;
            SummarizeButton.IsEnabled = !isBusy && hasText;
            ClearBridgeButton.IsEnabled = !isBusy && hasText;
            OpenFullReviewButton.IsEnabled = !isBusy;
            RetryOcrButton.IsEnabled = !isBusy;
            CloseButton.IsEnabled = true;
            CopyButton.IsEnabled = !isBusy;
            OpenFullResultButton.IsEnabled = !isBusy;
            BackButton.IsEnabled = !isBusy;
            ResultCloseButton.IsEnabled = true;
        }

        private bool EnsureTextAvailable()
        {
            if (!string.IsNullOrWhiteSpace(confirmedText))
                return true;

            ShowError(localizer.T("ClearBridge.OcrNoTextFound"));
            return false;
        }

        private bool NeedsReviewBeforeActionAnalysis()
        {
            if (confirmedText.Trim().Length < CrisisActionAnalysisService.MinInputLength)
                return true;

            return confirmedText.Contains("[unclear]", StringComparison.OrdinalIgnoreCase) ||
                   confirmedText.Contains("�", StringComparison.Ordinal);
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

        private string BuildClearBridgePreview(CrisisActionAnalysisResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"{localizer.T("ClearBridge.Priority")}: {LocalizePriority(result.Priority)}");
            builder.AppendLine(result.Summary);

            if (result.Actions.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine(localizer.T("ClearBridge.Actions"));
                foreach (var action in result.Actions.Take(3))
                    builder.AppendLine("- " + action.Task);
            }

            builder.AppendLine();
            builder.AppendLine(localizer.Format("ClearBridge.Quick.UnclearCount", result.UnclearItems.Count));
            return builder.ToString().Trim();
        }

        private string LocalizePriority(string priority)
        {
            var normalized = priority?.Trim().ToLowerInvariant() ?? "medium";
            return normalized is "low" or "medium" or "high" or "urgent"
                ? localizer.T("ClearBridge.Priority." + normalized)
                : localizer.T("ClearBridge.Priority.medium");
        }

        private string LocalizeClearBridgeError(string errorCode)
        {
            var key = "ClearBridge.Error." + errorCode;
            var value = localizer.T(key);
            return value == key ? localizer.T("ClearBridge.Error.Unexpected") : value;
        }

        private string LocalizeOcrError(string errorCode)
        {
            var key = "ClearBridge.Ocr.Error." + errorCode;
            var value = localizer.T(key);
            return value == key ? localizer.T("ClearBridge.Ocr.Error.Unexpected") : value;
        }

        private static string GetQuickOutputLanguage()
        {
            return AppLocalizationService.CurrentLanguage switch
            {
                AppLocalizationService.ChineseCode => ClearBridgeOutputLanguages.SimplifiedChinese,
                AppLocalizationService.ArabicCode => ClearBridgeOutputLanguages.Arabic,
                _ => ClearBridgeOutputLanguages.English
            };
        }

        private enum QuickActionResultMode
        {
            None,
            Translation,
            Summary,
            ClearBridge
        }
    }
}

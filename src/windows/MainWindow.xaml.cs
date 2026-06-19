using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LiveCaptionsTranslator.models.ClearBridge;
using LiveCaptionsTranslator.services.Localization;
using LiveCaptionsTranslator.services.Ocr;
using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;
using Button = Wpf.Ui.Controls.Button;

namespace LiveCaptionsTranslator
{
    public partial class MainWindow : FluentWindow
    {
        public OverlayWindow? OverlayWindow { get; set; } = null;
        public RollingSummaryOverlayWindow? RollingSummaryOverlayWindow { get; set; } = null;
        public bool IsAutoHeight { get; set; } = true;

        private HwndSource? hwndSource;
        private bool screenOcrHotkeyRegistered;
        private bool screenOcrCaptureInProgress;
        private readonly ScreenRegionCaptureService screenRegionCaptureService = new();
        private readonly WindowsLocalOcrProvider localOcrProvider = new();
        private OcrQuickActionWindow? ocrQuickActionWindow;

        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            ApplyLocalization();

            SourceInitialized += MainWindow_SourceInitialized;
            Closed += MainWindow_Closed;

            Loaded += (s, e) =>
            {
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
                RootNavigation.Navigate(typeof(CaptionPage));
                IsAutoHeight = true;
                CheckForFirstUse();
                CheckForUpdates();
            };

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            var windowState = WindowHandler.LoadState(this, Translator.Setting);
            if (windowState.Left <= 0 || windowState.Left >= screenWidth ||
                windowState.Top <= 0 || windowState.Top >= screenHeight)
            {
                WindowHandler.RestoreState(this, new Rect(
                    (screenWidth - 775) / 2, screenHeight * 3 / 4 - 167, 775, 167));
            }
            else
                WindowHandler.RestoreState(this, windowState);

            ToggleTopmost(Translator.Setting.MainWindow.Topmost);
            ShowLogCard(Translator.Setting.MainWindow.CaptionLogEnabled);
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource?.AddHook(WndProc);
            RefreshScreenOcrHotkey();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            CaptionPage.Instance?.ClearRollingSummaryForShutdown();
            RollingSummaryOverlayWindow?.Close();
            RollingSummaryOverlayWindow = null;
            UnregisterScreenOcrHotkey();
            hwndSource?.RemoveHook(WndProc);
            hwndSource = null;
        }

        public bool RefreshScreenOcrHotkey(bool showStatus = false)
        {
            if (hwndSource == null)
                return true;

            UnregisterScreenOcrHotkey();

            if (Translator.Setting?.ScreenOcrHotkeyEnabled != true)
            {
                if (showStatus)
                {
                    SnackbarHost.Show(
                        AppLocalizationService.T("Settings.ScreenOcrHotkey.Disabled"),
                        AppLocalizationService.T("Settings.ScreenOcrHotkey.Disabled.Detail"),
                        SnackbarType.Info,
                        timeout: 3,
                        closeButton: true);
                }

                return true;
            }

            var registration = ScreenOcrHotkeyService.Register(
                hwndSource.Handle,
                Translator.Setting.ScreenOcrHotkey);

            if (registration.Status == ScreenOcrHotkeyStatus.Registered)
            {
                screenOcrHotkeyRegistered = true;
                if (Translator.Setting.ScreenOcrHotkey != registration.NormalizedHotkey)
                    Translator.Setting.ScreenOcrHotkey = registration.NormalizedHotkey;

                if (showStatus)
                {
                    SnackbarHost.Show(
                        AppLocalizationService.T("Settings.ScreenOcrHotkey.Applied"),
                        registration.NormalizedHotkey,
                        SnackbarType.Success,
                        timeout: 3,
                        closeButton: true);
                }

                return true;
            }

            var titleKey = registration.Status == ScreenOcrHotkeyStatus.Conflict
                ? "Settings.ScreenOcrHotkey.Conflict"
                : "Settings.ScreenOcrHotkey.Invalid";
            var detailKey = registration.Status == ScreenOcrHotkeyStatus.Conflict
                ? "Settings.ScreenOcrHotkey.Conflict.Detail"
                : "Settings.ScreenOcrHotkey.Invalid.Detail";

            SnackbarHost.Show(
                AppLocalizationService.T(titleKey),
                AppLocalizationService.T(detailKey),
                SnackbarType.Error,
                timeout: 5,
                closeButton: true);
            return false;
        }

        private void UnregisterScreenOcrHotkey()
        {
            if (!screenOcrHotkeyRegistered || hwndSource == null)
                return;

            ScreenOcrHotkeyService.Unregister(hwndSource.Handle);
            screenOcrHotkeyRegistered = false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == ScreenOcrHotkeyService.WmHotkey &&
                wParam.ToInt32() == ScreenOcrHotkeyService.HotkeyId)
            {
                handled = true;
                _ = StartScreenOcrFromHotkeyAsync();
            }

            return IntPtr.Zero;
        }

        private async Task StartScreenOcrFromHotkeyAsync()
        {
            if (screenOcrCaptureInProgress)
            {
                SnackbarHost.Show(
                    AppLocalizationService.T("Settings.ScreenOcrHotkey.Busy"),
                    AppLocalizationService.T("Settings.ScreenOcrHotkey.Busy.Detail"),
                    SnackbarType.Warning,
                    timeout: 3,
                    closeButton: true);
                return;
            }

            screenOcrCaptureInProgress = true;
            var shouldRestoreMainWindow = IsVisible && WindowState != WindowState.Minimized;
            try
            {
                CloseQuickActionWindow();

                if (shouldRestoreMainWindow)
                    Hide();

                await Task.Delay(160);
                var input = screenRegionCaptureService.CaptureRegion(CancellationToken.None);
                if (shouldRestoreMainWindow)
                    Show();

                var result = await localOcrProvider.ExtractTextAsync(input, CancellationToken.None);
                ShowQuickActionWindow(input, result);
            }
            catch (OperationCanceledException)
            {
                if (shouldRestoreMainWindow)
                    Show();
            }
            catch (ClearBridgeOcrException ex)
            {
                if (shouldRestoreMainWindow || !IsVisible)
                    Show();

                SnackbarHost.Show(
                    AppLocalizationService.T("ClearBridge.OcrFailed"),
                    LocalizeOcrError(ex.ErrorCode),
                    SnackbarType.Error,
                    timeout: 4,
                    closeButton: true);
            }
            catch (Exception)
            {
                if (shouldRestoreMainWindow || !IsVisible)
                    Show();

                SnackbarHost.Show(
                    AppLocalizationService.T("ClearBridge.OcrFailed"),
                    AppLocalizationService.T("ClearBridge.Ocr.Error.CaptureFailed"),
                    SnackbarType.Error,
                    timeout: 4,
                    closeButton: true);
            }
            finally
            {
                screenOcrCaptureInProgress = false;
            }
        }

        private void ShowQuickActionWindow(
            ClearBridgeImageInput input,
            ClearBridgeOcrResult result)
        {
            ocrQuickActionWindow = new OcrQuickActionWindow(input, result);
            ocrQuickActionWindow.Closed += (sender, args) =>
            {
                if (ReferenceEquals(ocrQuickActionWindow, sender))
                    ocrQuickActionWindow = null;
            };
            ocrQuickActionWindow.Show();
        }

        private void CloseQuickActionWindow()
        {
            if (ocrQuickActionWindow == null)
                return;

            ocrQuickActionWindow.Close();
            ocrQuickActionWindow = null;
        }

        public async Task OpenClearBridgeOcrReviewAsync(
            ClearBridgeImageInput input,
            ClearBridgeOcrResult result,
            models.ClearBridge.ClearBridgeInputType inputType,
            string confirmedText)
        {
            var page = await NavigateToClearBridgePageAsync();
            await page.LoadOcrReviewAsync(input, result, inputType, confirmedText);
        }

        public async Task OpenClearBridgeOcrTranslationResultAsync(
            ClearBridgeImageInput input,
            ClearBridgeOcrResult result,
            models.ClearBridge.ClearBridgeInputType inputType,
            string confirmedText,
            string translatedText,
            string targetLanguage,
            string provider)
        {
            var page = await NavigateToClearBridgePageAsync();
            await page.LoadOcrTranslationResultAsync(
                input,
                result,
                inputType,
                confirmedText,
                translatedText,
                targetLanguage,
                provider);
        }

        public async Task OpenClearBridgeOcrSummaryResultAsync(
            ClearBridgeImageInput input,
            ClearBridgeOcrResult result,
            models.ClearBridge.ClearBridgeInputType inputType,
            string confirmedText,
            string summary,
            string provider)
        {
            var page = await NavigateToClearBridgePageAsync();
            await page.LoadOcrSummaryResultAsync(
                input,
                result,
                inputType,
                confirmedText,
                summary,
                provider);
        }

        public async Task OpenClearBridgeOcrAnalysisResultAsync(
            ClearBridgeImageInput input,
            ClearBridgeOcrResult result,
            models.ClearBridge.ClearBridgeInputType inputType,
            string confirmedText,
            models.ClearBridge.CrisisActionAnalysisOutcome outcome)
        {
            var page = await NavigateToClearBridgePageAsync();
            await page.LoadOcrAnalysisResultAsync(
                input,
                result,
                inputType,
                confirmedText,
                outcome);
        }

        public async Task OpenClearBridgeCaptionAnalysisAsync(
            IReadOnlyList<CaptionAnalysisSentence> sentences)
        {
            var page = await NavigateToClearBridgePageAsync();
            page.LoadCaptionAnalysis(sentences);
        }

        private async Task<ClearBridgePage> NavigateToClearBridgePageAsync()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            if (!IsVisible)
                Show();

            RootNavigation.Navigate(typeof(ClearBridgePage));
            await Dispatcher.InvokeAsync(
                () => { },
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            if (ClearBridgePage.Instance == null)
                throw new InvalidOperationException("ClearBridge page did not initialize.");

            Activate();
            return ClearBridgePage.Instance;
        }

        private static string LocalizeOcrError(string errorCode)
        {
            var key = "ClearBridge.Ocr.Error." + errorCode;
            var value = AppLocalizationService.T(key);
            return value == key ? AppLocalizationService.T("ClearBridge.Ocr.Error.Unexpected") : value;
        }

        private void ApplyLocalization()
        {
            Title = AppLocalizationService.T("App.Title");
            FlowDirection = AppLocalizationService.CurrentFlowDirection;
            MainContent.FlowDirection = AppLocalizationService.CurrentFlowDirection;
            RootNavigation.FlowDirection = AppLocalizationService.CurrentFlowDirection;
            DialogHostContainer.FlowDirection = AppLocalizationService.CurrentFlowDirection;

            TitleBarText.Text = AppLocalizationService.T("App.Title");
            CaptionNavItem.Content = AppLocalizationService.T("Nav.Caption");
            ClearBridgeNavItem.Content = AppLocalizationService.T("Nav.ClearBridge");
            SettingNavItem.Content = AppLocalizationService.T("Nav.Setting");
            HistoryNavItem.Content = AppLocalizationService.T("Nav.History");
            InfoNavItem.Content = AppLocalizationService.T("Nav.Info");

            CaptionLogButton.ToolTip = AppLocalizationService.T("Main.Tooltip.LogCards");
            LogOnlyButton.ToolTip = AppLocalizationService.T("Main.Tooltip.PauseTranslation");
            OverlayModeButton.ToolTip = AppLocalizationService.T("Main.Tooltip.OverlayWindow");
            TopmostButton.ToolTip = AppLocalizationService.T("Main.Tooltip.AlwaysOnTop");
        }

        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleTopmost(!this.Topmost);
        }

        private void OverlayModeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var symbolIcon = button?.Icon as SymbolIcon;

            if (OverlayWindow == null)
            {
                symbolIcon.Symbol = SymbolRegular.ClosedCaption24;
                symbolIcon.Filled = true;

                OverlayWindow = new OverlayWindow();
                OverlayWindow.SizeChanged +=
                    (s, e) => WindowHandler.SaveState(OverlayWindow, Translator.Setting);
                OverlayWindow.LocationChanged +=
                    (s, e) => WindowHandler.SaveState(OverlayWindow, Translator.Setting);

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                var windowState = WindowHandler.LoadState(OverlayWindow, Translator.Setting);
                if (windowState.Left <= 0 || windowState.Left >= screenWidth ||
                    windowState.Top <= 0 || windowState.Top >= screenHeight)
                {
                    WindowHandler.RestoreState(OverlayWindow, new Rect(
                        (screenWidth - 650) / 2, screenHeight * 5 / 6 - 135, 650, 135));
                }
                else
                    WindowHandler.RestoreState(OverlayWindow, windowState);

                OverlayWindow.Show();
            }
            else
            {
                symbolIcon.Symbol = SymbolRegular.ClosedCaptionOff24;
                symbolIcon.Filled = false;

                switch (OverlayWindow.OnlyMode)
                {
                    case CaptionVisible.TranslationOnly:
                        OverlayWindow.OnlyMode = CaptionVisible.SubtitleOnly;
                        OverlayWindow.OnlyMode = CaptionVisible.Both;
                        break;
                    case CaptionVisible.SubtitleOnly:
                        OverlayWindow.OnlyMode = CaptionVisible.Both;
                        break;
                }

                OverlayWindow.Close();
                OverlayWindow = null;
            }
        }

        public void ShowRollingSummaryOverlayWindow()
        {
            if (RollingSummaryOverlayWindow != null)
            {
                if (RollingSummaryOverlayWindow.WindowState == WindowState.Minimized)
                    RollingSummaryOverlayWindow.WindowState = WindowState.Normal;
                RollingSummaryOverlayWindow.Activate();
                return;
            }

            var overlayWindow = new RollingSummaryOverlayWindow();
            RollingSummaryOverlayWindow = overlayWindow;
            overlayWindow.SizeChanged +=
                (s, e) => WindowHandler.SaveState(overlayWindow, Translator.Setting);
            overlayWindow.LocationChanged +=
                (s, e) => WindowHandler.SaveState(overlayWindow, Translator.Setting);
            overlayWindow.Closed += (s, e) =>
            {
                if (ReferenceEquals(RollingSummaryOverlayWindow, s))
                    RollingSummaryOverlayWindow = null;
            };

            var savedState = WindowHandler.LoadState(overlayWindow, Translator.Setting);
            if (savedState.IsEmpty || !IsWindowStateOnScreen(savedState))
                WindowHandler.RestoreState(overlayWindow, GetDefaultRollingSummaryOverlayBounds());
            else
                WindowHandler.RestoreState(overlayWindow, savedState);

            overlayWindow.Show();
        }

        private Rect GetDefaultRollingSummaryOverlayBounds()
        {
            var workArea = SystemParameters.WorkArea;
            var width = 390.0;
            var height = Math.Min(620.0, Math.Max(360.0, workArea.Height - 80));
            var anchor = OverlayWindow != null && OverlayWindow.IsVisible
                ? new Rect(OverlayWindow.Left, OverlayWindow.Top, OverlayWindow.Width, OverlayWindow.Height)
                : new Rect(Left, Top, Width, Height);

            var left = anchor.Right + 12;
            if (left + width > workArea.Right)
                left = anchor.Left - width - 12;
            if (left < workArea.Left)
                left = workArea.Right - width - 20;

            var top = Math.Clamp(anchor.Top, workArea.Top + 20, workArea.Bottom - height - 20);
            return new Rect(left, top, width, height);
        }

        private static bool IsWindowStateOnScreen(Rect state)
        {
            var workArea = SystemParameters.WorkArea;
            return state.Width > 100 &&
                state.Height > 100 &&
                state.Left < workArea.Right - 80 &&
                state.Right > workArea.Left + 80 &&
                state.Top < workArea.Bottom - 80 &&
                state.Bottom > workArea.Top + 80;
        }

        private void LogOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var symbolIcon = button?.Icon as SymbolIcon;

            if (Translator.LogOnlyFlag)
            {
                Translator.LogOnlyFlag = false;
                symbolIcon.Filled = false;
            }
            else
            {
                Translator.LogOnlyFlag = true;
                symbolIcon.Filled = true;
            }

            Translator.ClearContexts();
        }

        private void CaptionLogButton_Click(object sender, RoutedEventArgs e)
        {
            Translator.Setting.MainWindow.CaptionLogEnabled = !Translator.Setting.MainWindow.CaptionLogEnabled;
            ShowLogCard(Translator.Setting.MainWindow.CaptionLogEnabled);
            CaptionPage.Instance?.AutoHeight();
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            var window = sender as Window;
            WindowHandler.SaveState(window, Translator.Setting);
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MainWindow_LocationChanged(sender, e);
            IsAutoHeight = false;
        }

        public void ToggleTopmost(bool enabled)
        {
            var button = TopmostButton as Button;
            var symbolIcon = button?.Icon as SymbolIcon;
            symbolIcon.Filled = enabled;
            this.Topmost = enabled;
            Translator.Setting.MainWindow.Topmost = enabled;
        }

        private void CheckForFirstUse()
        {
            if (!Translator.FirstUseFlag)
                return;

            RootNavigation.Navigate(typeof(SettingPage));
            LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);

            Dispatcher.InvokeAsync(() =>
            {
                var welcomeWindow = new WelcomeWindow
                {
                    Owner = this
                };
                welcomeWindow.Show();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private async Task CheckForUpdates()
        {
            if (Translator.FirstUseFlag)
                return;

            string latestVersion = string.Empty;
            try
            {
                latestVersion = await UpdateUtil.GetLatestVersion();
            }
            catch (Exception ex)
            {
                SnackbarHost.Show(AppLocalizationService.T("Main.Update.CheckFailed"), ex.Message, SnackbarType.Error,
                    timeout: 2, closeButton: true);

                return;
            }

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            var ignoredVersion = Translator.Setting.IgnoredUpdateVersion;
            if (!string.IsNullOrEmpty(ignoredVersion) && ignoredVersion == latestVersion)
                return;
            if (!string.IsNullOrEmpty(latestVersion) && latestVersion != currentVersion)
            {
                var dialog = new Wpf.Ui.Controls.MessageBox
                {
                    Title = AppLocalizationService.T("Main.Update.Title"),
                    Content = AppLocalizationService.Format("Main.Update.Content", latestVersion, currentVersion),
                    PrimaryButtonText = AppLocalizationService.T("Main.Update.Primary"),
                    CloseButtonText = AppLocalizationService.T("Main.Update.Ignore")
                };
                var result = await dialog.ShowDialogAsync();

                if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    var url = UpdateUtil.GitHubReleasesUrl;
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        SnackbarHost.Show(AppLocalizationService.T("Main.Update.OpenBrowserFailed"), ex.Message, SnackbarType.Error,
                            timeout: 2, closeButton: true);
                    }
                }
                else
                    Translator.Setting.IgnoredUpdateVersion = latestVersion;
            }
        }

        public void ShowLogCard(bool enabled)
        {
            if (CaptionLogButton.Icon is SymbolIcon icon)
            {
                if (enabled)
                    icon.Symbol = SymbolRegular.History24;
                else
                    icon.Symbol = SymbolRegular.HistoryDismiss24;
                CaptionPage.Instance?.CollapseTranslatedCaption(enabled);
            }
        }

        public void AutoHeightAdjust(int minHeight = -1, int maxHeight = -1)
        {
            if (minHeight > 0 && Height < minHeight)
            {
                Height = minHeight;
                IsAutoHeight = true;
            }

            if (IsAutoHeight && maxHeight > 0 && Height > maxHeight)
                Height = maxHeight;
        }
    }
}

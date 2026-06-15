# Phase 2 Language Test Report - Stabilization

Date: 2026-06-15

Branch: `feature/clearbridge-phase2-language`

## Scope

Phase 2 stabilizes multilingual UI behavior and keeps the competition build focused on the languages required for the demo.

Covered in this report:

- App UI languages: English, Simplified Chinese, Arabic.
- ClearBridge output languages: English, Simplified Chinese, Arabic.
- UI language changes now require an application restart.
- ClearBridge output language remains independent from UI language.
- Priority values are localized instead of showing internal values such as `high`.
- Existing Phase 1 mouse wheel limitation remains disclosed.

Spanish and French are removed from the user-visible competition build scope. Some lower-level constants or Mock branches may remain as future extension points, but they are not listed in the current picker or claimed as supported competition output languages.

## Crash Diagnosis

Windows Application Event Log showed the crash as:

- Exception type: `System.InvalidOperationException`
- Message: `集合被修改；枚举操作可能无法执行。`
- Wrapped in one entry by: `System.Reflection.TargetInvocationException`
- Stack trace summary:
  - `System.Windows.Documents.RangeContentEnumerator.MoveNext()`
  - `System.Linq.Enumerable.OfTypeIterator<TResult>(IEnumerable source)+MoveNext()`
  - `LiveCaptionsTranslator.services.Localization.AppLocalizationService.ApplyKnownText(DependencyObject element)`
  - repeated `AppLocalizationService.ApplyTo(DependencyObject root)`
  - `LiveCaptionsTranslator.SettingPage.ApplyLocalization()`
  - crash path 1: `SettingPage..ctor()` through WPF-UI navigation activation while entering Settings
  - crash path 2: `SettingPage.UiLanguageBox_SelectionChanged` -> `AppLocalizationService.SetLanguage`

Trigger steps observed from the log and manual report:

- Current page: Settings.
- User entered Settings or changed UI Language from Settings.
- The old runtime language switch raised a shared language event while loaded pages were traversing and updating WPF controls.
- During that traversal, `TextBlock.Inlines` / visual-tree collections were modified while being enumerated.

Root cause:

- The Phase 2 hot language switch tried to refresh already-loaded pages and FlowDirection in place.
- That refresh could run during page construction, Settings ComboBox initialization, navigation, or RTL/LTR layout changes.
- The shared traversal touched text runs and visual children while WPF was still mutating the same collections.

## Fix Summary

Implemented:

- Removed runtime `LanguageChanged` refresh subscriptions from loaded pages and windows.
- Changed UI language selection to save `UiLanguage` for the next restart.
- The application now applies saved UI language and FlowDirection only during startup/page construction.
- Added `_isInitializing` guard in Settings so ComboBox initialization does not trigger save/refresh side effects.
- Normalizes invalid language codes to English.
- Uses snapshot enumeration in localization traversal for safer startup localization.
- Added localized restart-required messages in English, Simplified Chinese, and Arabic.
- Localized ClearBridge priority display for `low`, `medium`, `high`, and `urgent`.
- Trimmed the user-visible ClearBridge output language picker to English, Simplified Chinese, and Arabic.

## Expected UI Language Behavior

When the user changes UI Language:

1. The selected language is saved to local `setting.json`.
2. The current visual tree is not hot-switched.
3. Main window FlowDirection is not changed at runtime.
4. A restart-required message is shown.
5. After restarting, the saved UI language is applied.

Restart message strings:

- English: `The interface language will be applied after restarting the application.`
- Simplified Chinese: `界面语言将在重新启动应用后生效。`
- Arabic: `سيتم تطبيق لغة الواجهة بعد إعادة تشغيل التطبيق.`

## Manual Acceptance Matrix

| Case | UI Language | Output Language | Provider | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| P2-MX-01 | English | English | Mock | Pass | UI labels English, Mock output English, priority displays localized as English. |
| P2-MX-02 | English | Arabic | Mock | Pass | UI labels English, Mock content Arabic, evidence source text remains original. |
| P2-MX-03 | Simplified Chinese | Simplified Chinese | Mock | Pass | UI labels Chinese, Mock output Chinese, priority displays localized as Chinese after restart. |
| P2-MX-04 | Simplified Chinese | Arabic | Mock | Pass | Mixed UI/output is expected: Chinese labels with Arabic generated content. |
| P2-MX-05 | Arabic | Arabic | Mock | Pass | Arabic UI and RTL layout passed manual regression. |
| P2-MX-06 | Arabic | English | Mock | Pass | Arabic labels with English generated content is expected. |

## Regression Checklist

| Check | Result | Notes |
| --- | --- | --- |
| English startup | Pass | Manual regression passed. |
| Chinese startup | Pass | Manual regression passed. |
| Arabic startup | Pass | Manual regression passed with RTL layout. |
| Settings page opens | Pass | No crash after stabilization. |
| Settings / ClearBridge / History page switching | Pass | Manual regression passed with no repeat crash observed. |
| UI language change prompt | Pass | Saves setting and shows localized restart-required message. |
| Restart applies saved language | Pass | Manual regression confirmed UI language changes apply after restart. |
| Caption page opens | Pass | No regression reported. |
| History page opens | Pass | Manual regression passed. |
| About page opens | Pass | No regression reported. |
| ClearBridge Mock analysis | Pass | Output picker lists only English, Simplified Chinese, Arabic. |
| OpenAI-compatible analysis | Not run locally | Requires user API configuration and network/service availability. |
| Save to History | Pass | History page switching and ClearBridge flow passed manual regression. |

## Validation Commands

Completed during stabilization:

- `dotnet restore .\LiveCaptionsTranslator.sln`: passed after allowing access to user NuGet configuration.
- `dotnet format .\LiveCaptionsTranslator.csproj --verify-no-changes --verbosity minimal`: passed after allowing access to user NuGet/MSBuild configuration.
- `dotnet build .\LiveCaptionsTranslator.sln -c Release --no-restore -v minimal`: passed with 0 warnings and 0 errors after code edits.
- `dotnet build .\LiveCaptionsTranslator.sln -c Release --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet publish .\LiveCaptionsTranslator.csproj -c Release -r win-x64 --self-contained true`: passed with 0 errors and existing nullable warnings.
- Fixed test package directory content was synchronized with the latest publish output under `test-build\ClearBridge-Latest`.

Manual regression completed after stabilization:

- English UI: passed.
- Simplified Chinese UI: passed.
- Arabic UI / RTL: passed.
- UI Language restart behavior: passed.
- Settings, ClearBridge, and History page switching: no crash observed.
- English, Simplified Chinese, and Arabic output: passed.
- Priority localization: passed.

## Known Issues

- ClearBridge result content remains fully viewable with the right-side scrollbar.
- Mouse wheel scrolling over some generated result areas remains unreliable and is not claimed fixed in Phase 2.
- Arabic UI / RTL passed manual regression; final demo screenshots/video still need to be captured for submission evidence.
- OpenAI-compatible Arabic output quality depends on the configured model following the prompt.

## Recommendation

Proceed to Phase 2 PR closeout and merge after final main verification. Do not claim the known mouse wheel limitation is fixed.

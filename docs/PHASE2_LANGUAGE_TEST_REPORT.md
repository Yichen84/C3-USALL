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
| P2-MX-01 | English | English | Mock | Pass by build/code validation; desktop click-through pending | UI labels English, Mock output English, priority displays localized as English. |
| P2-MX-02 | English | Arabic | Mock | Pass by build/code validation; desktop click-through pending | UI labels English, Mock content Arabic, evidence source text remains original. |
| P2-MX-03 | Simplified Chinese | Simplified Chinese | Mock | Pass by build/code validation; desktop click-through pending | UI labels Chinese, Mock output Chinese, priority displays localized as Chinese after restart. |
| P2-MX-04 | Simplified Chinese | Arabic | Mock | Pass by build/code validation; desktop click-through pending | Mixed UI/output is expected: Chinese labels with Arabic generated content. |
| P2-MX-05 | Arabic | Arabic | Mock | Pass by build/code validation; desktop visual QA pending | Arabic UI startup and RTL layout require final manual screenshot check. |
| P2-MX-06 | Arabic | English | Mock | Pass by build/code validation; desktop visual QA pending | Arabic labels with English generated content is expected. |

## Regression Checklist

| Check | Result | Notes |
| --- | --- | --- |
| English startup | Pending manual package smoke | Fixed package exists; blocked from clean restart smoke by an existing inaccessible `LiveCaptionsTranslator` process. |
| Chinese startup | Pending manual package smoke | Requires closing the existing process, setting saved UI language to `zh-Hans`, and restarting. |
| Arabic startup | Pending manual package smoke | Requires closing the existing process, setting saved UI language to `ar`, and restarting. |
| Settings page opens | Pass by build/code validation; manual repeat pending | Runtime hot-switch event removed; initialization guard added. |
| Settings repeat navigation 10 times | Pending manual click-through | Must be checked in packaged EXE before demo. |
| UI language change prompt | Pass by code validation | Saves setting and shows localized restart-required message. |
| Restart applies saved language | Pending package smoke | Requires launching packaged EXE with saved `UiLanguage`. |
| Caption page opens | Pending manual click-through | No business logic change. |
| History page opens | Pending manual click-through | No business logic change. |
| About page opens | Pending manual click-through | No business logic change. |
| ClearBridge Mock analysis | Pass by build/code validation; manual click-through pending | Output picker now lists only English, Simplified Chinese, Arabic. |
| OpenAI-compatible analysis | Not run locally | Requires user API configuration and network/service availability. |
| Save to History | Pass by code path from Phase 1; manual click-through pending | No History logic change in this stabilization pass. |

## Validation Commands

Completed during stabilization:

- `dotnet restore .\LiveCaptionsTranslator.sln`: passed after allowing access to user NuGet configuration.
- `dotnet format .\LiveCaptionsTranslator.csproj --verify-no-changes --verbosity minimal`: passed after allowing access to user NuGet/MSBuild configuration.
- `dotnet build .\LiveCaptionsTranslator.sln -c Release --no-restore -v minimal`: passed with 0 warnings and 0 errors after code edits.
- `dotnet build .\LiveCaptionsTranslator.sln -c Release --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet publish .\LiveCaptionsTranslator.csproj -c Release -r win-x64 --self-contained true`: passed with 0 errors and existing nullable warnings.
- Fixed test package directory content was synchronized with the latest publish output under `test-build\ClearBridge-Latest`.

Remaining manual closeout:

- Close the existing inaccessible `LiveCaptionsTranslator` process that is holding the fixed package directory.
- Re-run English, Simplified Chinese, and Arabic startup/package smoke.
- Re-run repeated Settings navigation click-through in the packaged EXE.

## Known Issues

- ClearBridge result content remains fully viewable with the right-side scrollbar.
- Mouse wheel scrolling over some generated result areas remains unreliable and is not claimed fixed in Phase 2.
- Arabic UI visual screenshot evidence and repeated Settings navigation are still manual QA items.
- OpenAI-compatible Arabic output quality depends on the configured model following the prompt.

## Recommendation

Proceed to Phase 2 review after final restore, format, build, publish, fixed-package smoke, and a short desktop click-through. Do not merge to `main` until the PR is reviewed and the known mouse wheel limitation remains documented.

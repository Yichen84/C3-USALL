# Phase 3 OCR Test Report

Date: 2026-06-15

Branch: `feature/clearbridge-phase3-ocr`

## Scope

Phase 3 adds one-time screen region capture, image upload, OCR review, Local OCR, optional AI OCR, and three independent post-OCR actions:

- Translate
- Summarize
- ClearBridge Analyze

OCR completion must stop at editable text review. It must not automatically call translation, summary, or ClearBridge analysis.

## Implementation Checks

| Area | Result | Notes |
| --- | --- | --- |
| Text mode preserved | Pass | Existing pasted-text ClearBridge flow remains available. |
| Screen region entry | Implemented, needs manual DPI QA | Uses a one-time WinForms overlay and in-memory screenshot capture. |
| Image upload entry | Implemented, needs manual file QA | Supports PNG, JPG, JPEG, and BMP, with a 10 MB input limit. |
| Local OCR | Build pass, needs OCR accuracy QA | Uses Windows OCR API (`Windows.Media.Ocr`); image is processed locally. |
| AI OCR | Build pass, needs configured-provider QA | Requires user confirmation before image upload. |
| OCR review | Pass by code inspection | Shows image preview, engine metadata, editable text, review warning, Retry, AI Retry, Clear, Cancel. |
| No automatic post-OCR action | Pass by code inspection | OCR completion only updates text preview and status. |
| Translate action | Build pass, needs manual provider QA | Uses confirmed OCR text, selected translation provider, and selected target language. |
| Summary action | Build pass, needs configured OpenAI QA | Produces plain summary only; no priority, checklist, or ClearBridge fields. |
| ClearBridge OCR action | Build pass, needs manual UI QA | Uses confirmed OCR text and stores `FeatureType = ClearBridge OCR`. |
| History classification | Pass by code inspection | Adds `OCR Translation`, `OCR Summary`, and `ClearBridge OCR` feature types plus OCR metadata fields. |
| Image persistence | Pass by code inspection | Raw image bytes are held in memory and not saved to History or logs. |
| API key logging | Pass by code inspection | Authorization headers and keys are not logged. |

## Manual Test Fix Pass - 2026-06-15

| Area | Result | Notes |
| --- | --- | --- |
| Image upload entry visibility | Pass by code/build inspection | `Capture Screen Region` and `Upload Image` are now separated as the primary OCR entry buttons at the top of the OCR input area. |
| Image upload flow | Pass by code/build inspection, pending file QA | Upload opens PNG/JPG/JPEG/BMP files, shows preview, writes source file name and image size in metadata, runs Local OCR, and shares the same editable OCR Review flow as screen capture. |
| Cancel file selection | Pass by code inspection | Canceling the file dialog returns without changing state or showing an error. |
| Damaged/invalid image handling | Pass by code inspection | Invalid files are caught and shown as localized `InvalidImage` OCR failure messages without crashing. |
| No-text image handling | Pass by code inspection | Empty OCR text sets `No text found` plus the review-next-action prompt so the user can retry, edit, clear, or cancel. |
| Text/OCR input mode cleanup | Pass by code/build inspection | Text mode shows Notice Text plus Example/Clear/Analyze with ClearBridge. OCR modes hide those text-mode controls and show OCR Review actions: Translate, Summarize, Analyze with ClearBridge, Retry OCR, Retry with AI OCR, Clear, Cancel. |
| Global one-time screen OCR hotkey | Build pass, pending desktop hotkey QA | Added configurable global hotkey registration through Windows `RegisterHotKey`; default is `Ctrl + Alt + O`. The hotkey navigates to ClearBridge and starts the existing one-time screen capture flow. |
| Hotkey configurability | Pass by code/build inspection | Settings shows Screen OCR Hotkey, enable/disable switch, editable hotkey field, and Apply button. Valid changes persist in `setting.json` and apply without changing OCR business logic. |
| Hotkey conflict handling | Pass by code inspection | If Windows rejects registration, the app shows a localized conflict error and does not fall back to another hidden hotkey. Invalid combinations are rejected before save. |
| Capture already active | Pass by code inspection | A second hotkey press while a capture is active shows a busy prompt instead of starting another capture. |
| History OCR Translation | Pass by code inspection | Writes `FeatureType = OCR Translation`, `InputType`, `OcrEngine`, `OcrWasCloudBased`, and `OcrTextEdited` into `TranslationHistory`; no image/Base64 is saved. |
| History OCR Summary | Pass by code inspection | Writes `FeatureType = OCR Summary` and the same OCR metadata fields into `TranslationHistory`; no image/Base64 is saved. |
| History ClearBridge OCR | Pass by code inspection | Writes `FeatureType = ClearBridge OCR` plus OCR metadata into `ClearBridgeHistory` and the compatibility History row; no image/Base64 is saved. |
| DPI / multi-monitor behavior | Code path supports virtual screen, pending manual QA | Screen selection uses `SystemInformation.VirtualScreen`, which covers multi-monitor and negative-coordinate layouts. Physical 125%/150% DPI and dual-monitor validation still needs manual testing. |

## Required Manual Scenarios

| Case | Provider | Language | Result | Main Issue |
| --- | --- | --- | --- | --- |
| P3-Screen-EN-01 | Local OCR | English UI | Pending manual | Verify school weather notice OCR keeps `12:30 PM` and bus uncertainty. |
| P3-Screen-ZH-01 | Local OCR | Chinese UI | Pending manual | Verify Chinese notice OCR and review/edit flow. |
| P3-Screen-AR-01 | Local OCR | Arabic UI / RTL | Pending manual | Verify Arabic text extraction and UI layout. |
| P3-Image-PNG-01 | Local OCR | English output | Pending manual | Verify PNG upload and editable review text. |
| P3-Image-JPG-01 | Local OCR | Simplified Chinese output | Pending manual | Verify JPG upload and translation action. |
| P3-Image-BMP-01 | Local OCR | Arabic output | Pending manual | Verify BMP upload and ClearBridge action. |
| P3-AI-01 | AI OCR | English UI | Pending configured-provider QA | Verify cloud confirmation appears before upload. |
| P3-AI-02 | AI OCR | Chinese UI | Pending configured-provider QA | Verify canceling upload makes no API request. |
| P3-Action-A | Translation Provider | Chinese target | Pending manual | Translation result must not show Priority or Checklist. |
| P3-Action-B | OpenAI-compatible Summary | English output | Pending configured-provider QA | Summary result must not show structured action fields. |
| P3-Action-C | ClearBridge Provider | English / Chinese / Arabic | Pending manual | ClearBridge result must show action fields and source evidence. |
| P3-Cancel | None | English UI | Pending manual | Cancel must not trigger provider calls. |
| P3-Retry | Local / AI OCR | English UI | Pending manual | Retry must not repeat old post-processing requests. |
| P3-Hotkey-01 | Local OCR | English UI | Pending manual | Press `Ctrl + Alt + O` from another app; verify one-time region capture opens and returns to ClearBridge OCR Review. |
| P3-Hotkey-02 | None | Settings | Pending manual | Change hotkey, restart, verify persisted setting and conflict message for an occupied shortcut. |
| P3-Mode-01 | None | English / Chinese / Arabic UI | Pending manual | Verify text mode and OCR modes do not show duplicate Notice Text/OCR Review controls. |

## Build Verification

- `dotnet restore .\LiveCaptionsTranslator.sln`: passed.
- `dotnet format .\LiveCaptionsTranslator.csproj --verify-no-changes --verbosity minimal`: passed.
- `dotnet build .\LiveCaptionsTranslator.sln -c Release --no-restore`: passed with 0 errors and existing nullable warnings.
- `dotnet publish .\LiveCaptionsTranslator.csproj -c Release -r win-x64 --self-contained true`: passed with 0 errors and existing nullable warnings.
- Latest publish output: `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish`.
- Fixed local test package: `test-build\ClearBridge-Latest\LiveCaptionsTranslator.exe`.
- Fixed package launch smoke: passed; the app process started successfully and was closed.

## Known Limitations

- Screen capture DPI and multi-monitor behavior require manual QA on 125%, 150%, and negative-coordinate monitor layouts.
- Global hotkey behavior requires physical desktop QA, especially while another app has focus.
- Windows Local OCR accuracy depends on installed Windows OCR language capabilities and image quality.
- Arabic OCR accuracy may be lower than English.
- AI OCR requires a configured OpenAI-compatible model that supports vision input.
- The existing Phase 1 mouse wheel issue over some generated result areas remains a known issue.

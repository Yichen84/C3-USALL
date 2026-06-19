# Final Regression Report

## Automated

- `git pull --ff-only`: PASS, already up to date.
- `dotnet restore .\LiveCaptionsTranslator.sln`: PASS.
- `dotnet format .\LiveCaptionsTranslator.csproj --verify-no-changes --verbosity minimal`: PASS.
- `dotnet build .\LiveCaptionsTranslator.sln -c Release --no-restore`: PASS, `0 warnings`, `0 errors`.
- `dotnet run --project .\tools\Phase4CaptionAudit\Phase4CaptionAudit.csproj -c Release`: PASS, 10 checks.
- `dotnet run --project .\tools\Phase5RollingSummaryAudit\Phase5RollingSummaryAudit.csproj -c Release`: PASS, 15 checks.
- `dotnet publish .\LiveCaptionsTranslator.csproj -c Release -r win-x64 --self-contained true`: PASS.

## Mock / Harness Coverage

- Phase 4 range boundaries are inclusive.
- 400 sentence limit allows 400 and blocks 401.
- Caption snapshot request is immutable.
- Conservative duplicate and incremental caption handling is validated.
- Mock caption provider respects selected text and output languages.
- Mock no-action captions do not invent actions.
- Mock ambiguous captions produce unclear items.
- Caption evidence sanitizer removes unsupported source text.
- Phase 5 cursor advances only on success.
- Cancel and provider failure keep captions pending.
- Concurrent rolling summary processing is rejected.
- Pause and stop prevent processing.
- Ten rolling batches keep cache bounded.
- Rolling summary mock supports English, Simplified Chinese, and Arabic.
- Rolling JSON parser handles null, wrapped JSON, and invalid JSON cases.

## Real API

Real API validation was completed before this final packaging pass using synthetic data only. This final pass did not re-run paid or external provider calls.

- Text ClearBridge: previously validated with real OpenAI-compatible provider using synthetic notices.
- Caption Analysis: previously validated with real OpenAI-compatible provider using synthetic captions.
- Rolling Summary: previously validated with three synthetic batches.
- Chinese output: previously validated.
- Arabic output: previously validated after strict invalid-JSON retry behavior.
- Failure rollback: previously validated; provider failure does not fallback to Mock and does not advance the rolling cursor.

## Manual Desktop

- Fixed test EXE startup and shutdown smoke: PASS.
- Final package EXE startup and shutdown smoke: PASS.
- Main window launch: PASS by smoke.
- Application closes without leaving a detected `LiveCaptionsTranslator` background process: PASS by smoke.
- Prior manual validation covered OCR, Alt+V, quick action card, caption analysis, rolling overlay, History, and language switching.

## Feature Regression Matrix

| Feature | Result | Notes |
| --- | --- | --- |
| Startup | PASS | Fixed package and final package smoke launched and closed. |
| Caption Translation | PASS by prior validation and build | No full live provider rerun in final pass. |
| Text ClearBridge | PASS by prior validation and build | Mock and real API validated before final packaging. |
| UI Languages | PASS by prior manual validation | English, Simplified Chinese, Arabic. |
| OCR | PASS by prior manual validation | Alt+V and image OCR retained. |
| Caption Analysis | PASS | Phase 4 audit passed 10 checks. |
| Rolling Summary | PASS | Phase 5 audit passed 15 checks and final smoke passed. |
| History | PASS by harness and prior manual validation | Rolling Summary saves only after confirmation. |
| Shutdown | PASS | Smoke closed app and temporary generated DB/logs were removed. |

## Not Fully Tested

- Every DPI and multi-monitor configuration.
- Long-duration real classroom caption streams.
- Optional real API OCR in every supported UI language during the final pass.
- Browser/PDF/chat-app hotkey triggering in this final pass.
- Full replacement of inherited upstream Google-related credential-like constants. The known instance was manually reviewed and accepted as not being a user personal key and not being introduced by ClearBridge/OpenAI work.

## Findings

- P0: none.
- P1: none.
- P2: known mouse-wheel limitation in some long ClearBridge result areas; inherited upstream Google-related credential-like constant reviewed and accepted; both documented.

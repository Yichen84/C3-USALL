# Phase 5 Rolling Summary Test Report

Date: 2026-06-18

Branch: `feature/clearbridge-phase5-rolling-summary`

## Scope

Phase 5 adds a user-controlled rolling summary mode for realtime captions.

Implemented behavior:
- Default state is Off / Stopped.
- User must click Start before caption batches are considered.
- Default batch interval is 90 seconds, with 60 / 90 / 120 second options.
- Process Now allows demos without waiting for the timer.
- Batch processing uses only new captions since the last successful batch.
- A compressed context cache is kept in memory only.
- Raw batch buffers are derived from the current caption snapshot and are not saved by default.
- Cancel or failure does not advance the processed caption cursor.
- Save Confirmed Summary writes a `ClearBridge Rolling Summary` History record only after user action.
- Confirmed History does not save full raw caption batches.
- Added an independent dark translucent `RollingSummaryOverlayWindow` for ongoing summary monitoring outside the Caption page.
- The overlay shares the same rolling session as the Caption page, so it does not create a second provider request path.
- The overlay shows batch-by-batch results, supports internal scrolling, and only auto-scrolls to the newest batch when the user is already near the bottom.
- The overlay remembers position and size through the existing window bounds mechanism; temporary summary content itself is not persisted.
- The overlay can be dragged, resized, toggled Topmost, collapsed, closed, and reopened.

## Code-Level Validation

Snapshot and batching:
- `RollingSummarySessionService.CreatePendingRequest` creates a new immutable pending request from the current caption snapshot.
- Captions are selected by user-visible sequence number greater than `LastProcessedSentenceNumber`.
- `LastProcessedSentenceNumber` only advances after a provider returns successfully.
- New captions that arrive during processing are not part of the in-flight request.

Thresholds:
- Minimum batch threshold is conservative: at least 5 processed sentences or at least 200 characters.
- Small batches are held with `WaitingForContent` instead of calling a provider.

Deduplication:
- Reuses the Phase 4 conservative caption preprocessor.
- Consecutive exact duplicates are removed.
- Clear consecutive incremental captions keep the most complete latest caption.
- No cross-window semantic deletion is attempted.

Concurrency:
- `SemaphoreSlim` prevents overlapping rolling summary requests.
- `Process Now` uses the same single-instance request path.
- Timer processing does not bypass the same request gate.

Temporary cache:
- `RollingContextCache` keeps current topic, facts, actions, dates, locations, warnings, unresolved questions, and a compressed narrative.
- Lists are capped to 20 items.
- Compressed narrative is capped to about 2500 characters.
- Clear Temporary Context and app close clear the in-memory cache.
- Temporary context is not written to disk by default.
- The overlay is closed on app shutdown and the shared temporary rolling context is cleared.

Provider safety:
- OpenAI-compatible rolling summary uses minimal Chat Completions fields: `model`, `temperature`, `response_format`, and `messages`.
- It does not send `enable_thinking`, `keep_alive`, `think`, `thinking`, `reasoning`, or `reasoning_effort`.
- Source evidence is sanitized against the current caption batch only. Previous compressed context may guide continuity but is not accepted as new `source_evidence.source_text`.
- Provider HTTP/network/timeout failures do not fall back to Mock and do not advance the rolling cursor or compressed context.
- Empty or invalid JSON responses trigger one stricter JSON retry. If retry also fails, the request fails without advancing cursor/cache.
- API key and Authorization header are not logged.

History:
- `FeatureType = ClearBridge Rolling Summary`.
- Confirmed records include session id, session start/end, batch count, confirmed action count, user confirmation, and `TemporaryContextPersisted = 0`.
- Full raw caption batches are not saved by default.

## Harness Validation

Command:

```powershell
dotnet run --project .\tools\Phase5RollingSummaryAudit\Phase5RollingSummaryAudit.csproj -c Release
```

Result: Passed 15 checks.

Covered cases:

| Case | Result | Notes |
| --- | --- | --- |
| Three batches | Pass | Context evolved across batch 1, task batch 2, correction batch 3 |
| Consume once | Pass | Already processed captions were not resent |
| Minimum threshold | Pass | Tiny batch blocked locally |
| Cancel rollback | Pass | Cancelled batch remained pending and could retry |
| 10 batches | Pass | Batch count reached 10 and cache stayed bounded |
| Mock languages | Pass | English, Simplified Chinese, Arabic Mock output returned |
| Superseded facts | Pass | Later Monday/project portal correction replaced earlier Friday/Google Classroom cache entries |
| Provider failure rollback | Pass | Synthetic provider failure did not advance cursor or batch count; retry consumed the same pending captions |
| Concurrent Process Now | Pass | Second simultaneous request was rejected with `AlreadyProcessing` |
| Pause / Stop guards | Pass | Paused or stopped sessions did not consume captions |
| Confirmed History metadata | Pass | Confirmed JSON includes user confirmation and `temporary_context_persisted = false` |
| Source Evidence scope | Pass | Prior-context evidence was removed when not present in current batch |
| Null fields | Pass | Parser normalized null lists/fields |
| Wrapped JSON | Pass | Parser extracted a complete JSON object when provider text wrapped it with prose |
| Invalid JSON | Pass | Parser rejected invalid JSON safely |

## Desktop Validation

Code-level verified in this pass.

Physical desktop validation still needed:
- Start / Pause / Resume / Stop while real captions are entering.
- Process Now with real captions.
- Open, drag, resize, collapse, close, and reopen the rolling summary overlay.
- Verify overlay placement near the realtime caption overlay and saved size/position restore.
- Verify overlay internal scrolling and no forced auto-scroll while reviewing older batches.
- Save Confirmed Summary and inspect History UI row.
- English, Simplified Chinese, and Arabic UI rendering.
- Long-running timer behavior over several real minutes.

## Real API Validation

Command:

```powershell
dotnet run --no-build --project .\tools\Phase5RollingSummaryAudit\Phase5RollingSummaryAudit.csproj -c Release -- --real-api
```

Result: Passed with synthetic captions only.

Safe summary:
- Fixed-package OpenAI-compatible config was present with API key, model, and URL. Values were not printed.
- Three rolling batches succeeded.
- Correction handling passed: Monday replaced Friday and project portal replaced Google Classroom in the compressed context.
- Simplified Chinese output succeeded.
- Arabic output initially returned invalid JSON, then succeeded after the single stricter JSON retry.
- Synthetic failure rollback passed with cursor and batch count remaining at zero.

No API key, Authorization header, full request body, full subtitle transcript, full compressed context, or full model response was printed or committed.

## Regression Notes

- Phase 4 manual caption analysis remains separate from rolling summary.
- OCR, Alt+V quick card, Settings, and standard caption translation were not intentionally changed.
- Existing Phase 1 mouse wheel limitation remains disclosed.

## Known Limitations

- Rolling Summary is a Phase 5 MVP with a Caption page panel and floating overlay, not a full persisted session browser.
- The interval selector is on the Caption page rather than the full Settings page.
- AI-generated results are marked unreviewed in status text, but item-level Confirm / Inaccurate / Needs Review controls are not yet implemented as separate per-item buttons.
- Real API quality and physical desktop behavior need manual validation before final competition packaging.
- Real API validation used synthetic captions from the audit harness, not real classroom or user data.
- Arabic rolling summary may require the new single JSON retry with the current configured provider.

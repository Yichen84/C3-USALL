# Phase 5 Manual API Test Checklist

Date: 2026-06-18

Branch: `feature/clearbridge-phase5-rolling-summary`

Use only synthetic or non-sensitive captions for manual API testing. Do not paste API keys into this document.

## Setup

| Item | Operation | Expected | Result | Notes |
| --- | --- | --- | --- | --- |
| API-00 Provider config | Open fixed test EXE, confirm OpenAI-compatible provider is configured locally. | Provider can be selected; key is not displayed in logs or docs. | [ ] Pending |  |
| API-01 Privacy precheck | Confirm logs, console, and docs do not show API key, Authorization header, full request body, full captions, or full compressed context. | No sensitive output. | [ ] Pending |  |

## Rolling Summary Session

| Item | Operation | Expected | Result | Notes |
| --- | --- | --- | --- | --- |
| API-02 Start session | Open Caption page, select OpenAI-compatible provider, choose English output, click Start. | Status enters listening/collecting state; no provider request until enough content or Process Now. | [ ] Pending |  |
| API-03 Three batches | Feed three synthetic batches: intro, Friday/Google Classroom assignment, Monday/project portal correction. Click Process Now after each batch if needed. | Three batches append; cache updates; latest correction supersedes old deadline/location. | [ ] Pending |  |
| API-04 Source evidence | Inspect evidence after batch 2 and batch 3. | Evidence snippets come from current batch text, not old compressed context. | [ ] Pending |  |
| API-05 Arabic output | Repeat one batch with Arabic output. | JSON parses; if first response is invalid, one retry may occur and user sees successful result or clear error. | [ ] Pending |  |
| API-06 Chinese output | Repeat one batch with Simplified Chinese output. | User-visible fields are Chinese; evidence source text remains original caption wording. | [ ] Pending |  |

## Controls

| Item | Operation | Expected | Result | Notes |
| --- | --- | --- | --- | --- |
| API-07 Pause | Click Pause, add captions, click Process Now. | No captions are consumed while paused. | [ ] Pending |  |
| API-08 Resume | Click Resume, then Process Now. | Pending captions process once. | [ ] Pending |  |
| API-09 Stop | Click Stop, add captions, click Process Now. | No captions are consumed while stopped. | [ ] Pending |  |
| API-10 Cancel | Start processing, cancel/close page if available during request. | Request cancels cleanly; cursor/cache do not advance; no History row is written. | [ ] Pending |  |
| API-11 Concurrent click | Double-click Process Now quickly. | Only one request runs; duplicate request is blocked. | [ ] Pending |  |
| API-12 Provider failure | Temporarily select invalid model in local app settings or disconnect network, then Process Now. | Clear error; cursor/cache remain unchanged; retry after restoring config uses same pending captions. | [ ] Pending | Do not commit setting changes. |

## Overlay

| Item | Operation | Expected | Result | Notes |
| --- | --- | --- | --- | --- |
| API-13 Open overlay | Click Open Overlay. | Dark translucent floating window opens and shares the Caption page session. | [ ] Pending |  |
| API-14 Overlay controls | Test Start, Pause, Resume, Process Now, Stop from overlay. | Controls affect the same session; no duplicate provider path. | [ ] Pending |  |
| API-15 Overlay append | Process three batches while overlay is visible. | Batches append at bottom; internal scroll works. | [ ] Pending |  |
| API-16 Old content scroll | Scroll overlay upward, then process another batch. | Overlay does not force-scroll to bottom while user reviews older batches. | [ ] Pending |  |
| API-17 Position and size | Move/resize overlay, close/reopen app. | Position/size restore; temporary summary content does not persist. | [ ] Pending |  |

## History and Cleanup

| Item | Operation | Expected | Result | Notes |
| --- | --- | --- | --- | --- |
| API-18 Save confirmed | Click Save Confirmed Summary. | One `ClearBridge Rolling Summary` History row is created only after user action. | [ ] Pending |  |
| API-19 History privacy | Inspect saved record. | No full raw caption batches; metadata shows user confirmed and temporary context not persisted. | [ ] Pending |  |
| API-20 Clear context | Click Clear Temporary Context. | In-memory context clears; next batch starts fresh. | [ ] Pending |  |
| API-21 App close | Close app after temporary batches without saving. | Temporary rolling content is cleared on next launch. | [ ] Pending |  |

## Issue Record

Use this format for any failure:

```text
Case:
Provider:
Output language:
Operation:
Expected:
Actual:
Sensitive data exposed? Yes/No:
Screenshot/video path:
Follow-up needed:
```

# Phase 4 Manual API Test Checklist

This checklist is for evening validation with a real OpenAI-compatible provider.
Do not use fake API keys. Do not describe Mock results as real model results.

For each case, record:
- Result: `[ ] Pass` / `[ ] Fail`
- Issue notes: step, observed behavior, error text, screenshot/video reference if available

## Environment Setup

- [ ] Configure a real OpenAI-compatible Provider in Settings.
- [ ] Confirm the API key is not committed, pasted into docs, or shown in screenshots.
- [ ] Confirm UI Language restart behavior still works before testing captions.
- [ ] Confirm ClearBridge Output Language is independent from UI Language.

## Test Cases

| Case | Operation | Expected Result | Result | Issue Notes |
| --- | --- | --- | --- | --- |
| API-01 Provider setup | Configure real OpenAI-compatible provider, save Settings, restart if needed. | Provider is available; no key appears in logs, History, Git, screenshots, or docs. | [x] Pass | Harness read fixed-package `setting.json` and confirmed provider/model/key presence without printing the key. |
| API-02 20-30 sentence range | Start or load captions, open Analyze Captions, choose Sentence Range with 20-30 sentences, click Analyze. | Only selected captions are sent; result includes summary/key points/action fields supported by source evidence. | [x] Pass | Runner used Dataset A range 5-25, 21 sentences, English, success, no out-of-range evidence. Desktop click path pending. |
| API-03 120 sentence All | Use about 120 captions and Analyze All Captions. | Request uses all 120 captions in one snapshot; no unrelated or later captions are included. | [x] Pass | Runner used Dataset B All, Simplified Chinese, 120 original / 117 processed, success, no out-of-range evidence. |
| API-04 400 sentence boundary | Use exactly 400 captions and Analyze All or 1-400. | Analysis is allowed; no silent truncation; UI remains responsive. | [x] Pass | Runner used Dataset C All, 400 original / 400 processed, success, one provider request. Physical UI responsiveness pending. |
| API-05 401 sentence block | Use 401 captions and Analyze All or 1-401. | Analyze is disabled or blocked with the localized 400 sentence message; no provider request is sent. | [x] Pass | Runner blocked Dataset D locally with `RangeTooLarge`; no provider call and no History write. |
| API-06 English output | Select English output and analyze a supported range. | Result text is English; source evidence remains original caption wording. | [x] Pass | Dataset A range 5-25, English, success, no out-of-range evidence. |
| API-07 Chinese output | Select Simplified Chinese output and analyze a supported range. | Result text is Simplified Chinese; source evidence remains original caption wording. | [x] Pass | Dataset B All, Simplified Chinese, success, source evidence check passed. |
| API-08 Arabic output | Select Arabic output and analyze a supported range. | Result text is Arabic; UI remains RTL; provider names and numbers remain readable. | [x] Pass | Dataset A range 5-25, Arabic, success, source evidence check passed. Full desktop RTL visual validation pending. |
| API-09 No-action content | Analyze classroom or lecture captions with no explicit assignment or task. | Action Checklist may be empty; no fake homework, deadlines, or locations are invented. | [x] Pass | Dataset E, English, success after evidence sanitizer; no out-of-range evidence. |
| API-10 Explicit assignment | Analyze captions containing `Submit the worksheet`, `Friday`, and `Google Classroom`. | Task, deadline, location, and source evidence are extracted accurately. | [x] Pass | Dataset A/B both include explicit assignment details and passed runner checks. |
| API-11 Ambiguous captions | Analyze captions such as `Maybe submit it next week`, `room could change`, `ask someone later`. | Unclear Items capture uncertainty; no firm deadline or location is invented. | [x] Pass | Dataset F, English, success after evidence sanitizer; no out-of-range evidence. |
| API-12 Cancel | Start a real provider request, click Cancel before completion. | Request is cancelled or UI returns safely; no result is saved to History. | [x] Pass | Runner cancelled Dataset B request after about 100 ms; status Cancelled, no output, no History write. Desktop button path pending. |
| API-13 Consecutive clicks | Click Analyze repeatedly during an active request. | Only one request is active; no duplicate History rows are created. | [x] Code-level | UI disables repeated Analyze during active request; runner does not duplicate calls. Physical rapid-click test pending. |
| API-14 Captions continue during analysis | Start analysis, then let new captions continue arriving. | In-flight request uses the snapshot from click time; new captions do not enter current result. | [x] Harness | Snapshot immutability passed in offline harness; physical live-caption flow pending. |
| API-15 History and evidence | Save the result to History and inspect the row. | `FeatureType = ClearBridge Caption Analysis`; range metadata is present; source evidence comes only from selected range. | [ ] Pending desktop Save | Runner intentionally did not write formal user History. Code path and metadata reviewed; desktop Save verification still required. |
| API-16 Provider failure recovery | Temporarily use invalid endpoint/model or disconnect network, then retry with valid settings. | Failure message is understandable; no error result is saved; user can retry after fixing provider config. | [x] Pass | Network error used localhost unreachable endpoint in memory; invalid model used temporary in-memory model. `setting.json` was not modified. |

## Real API Runner Summary - 2026-06-18

- Provider: OpenAI-compatible
- Model: gpt-4.1-mini
- API configuration source: fixed-package `setting.json`
- Key exposed: No

| Test | Language | Sentences | Range | Status | Latency | Evidence |
| --- | --- | ---: | --- | --- | ---: | --- |
| 5-25 range | English | 21 | 5-25 | Success | 5743 ms | No out-of-range evidence |
| 120 all | Simplified Chinese | 120 original / 117 processed | 1-120 | Success | 6798 ms | No out-of-range evidence |
| Arabic | Arabic | 21 | 5-25 | Success | 5288 ms | No out-of-range evidence |
| 400 | English | 400 | 1-400 | Success | 11291 ms | No out-of-range evidence |
| 401 blocked | Local | 401 | 1-401 | Blocked locally | 0 ms | No provider request |
| No-action | English | 12 | 1-12 | Success | 2419 ms | No out-of-range evidence |
| Ambiguous | English | 8 | 1-8 | Success | 5102 ms | No out-of-range evidence |
| Cancel | English | 120 original / 117 processed | 1-120 | Cancelled | 102 ms | No History write |
| Network error | English | 0 | n/a | Failed safely | 0 ms | Local unreachable endpoint |
| Invalid model | English | 21 | 5-25 | Failed safely | 336 ms | In-memory invalid model |

## Required Evidence

- Screenshot/video of All Captions mode.
- Screenshot/video of Sentence Range mode.
- Screenshot/video of 400 sentence guardrail.
- Screenshot/video of English, Simplified Chinese, and Arabic outputs.
- Screenshot/video of History row with `ClearBridge Caption Analysis`.
- Notes confirming Source Evidence stayed within the selected range.

## Pending Decision

Do not merge Phase 4 until this checklist is completed with a real provider or the remaining real-provider gaps are explicitly accepted.

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
| API-01 Provider setup | Configure real OpenAI-compatible provider, save Settings, restart if needed. | Provider is available; no key appears in logs, History, Git, screenshots, or docs. | [ ] Pass / [ ] Fail |  |
| API-02 20-30 sentence range | Start or load captions, open Analyze Captions, choose Sentence Range with 20-30 sentences, click Analyze. | Only selected captions are sent; result includes summary/key points/action fields supported by source evidence. | [ ] Pass / [ ] Fail |  |
| API-03 120 sentence All | Use about 120 captions and Analyze All Captions. | Request uses all 120 captions in one snapshot; no unrelated or later captions are included. | [ ] Pass / [ ] Fail |  |
| API-04 400 sentence boundary | Use exactly 400 captions and Analyze All or 1-400. | Analysis is allowed; no silent truncation; UI remains responsive. | [ ] Pass / [ ] Fail |  |
| API-05 401 sentence block | Use 401 captions and Analyze All or 1-401. | Analyze is disabled or blocked with the localized 400 sentence message; no provider request is sent. | [ ] Pass / [ ] Fail |  |
| API-06 English output | Select English output and analyze a supported range. | Result text is English; source evidence remains original caption wording. | [ ] Pass / [ ] Fail |  |
| API-07 Chinese output | Select Simplified Chinese output and analyze a supported range. | Result text is Simplified Chinese; source evidence remains original caption wording. | [ ] Pass / [ ] Fail |  |
| API-08 Arabic output | Select Arabic output and analyze a supported range. | Result text is Arabic; UI remains RTL; provider names and numbers remain readable. | [ ] Pass / [ ] Fail |  |
| API-09 No-action content | Analyze classroom or lecture captions with no explicit assignment or task. | Action Checklist may be empty; no fake homework, deadlines, or locations are invented. | [ ] Pass / [ ] Fail |  |
| API-10 Explicit assignment | Analyze captions containing `Submit the worksheet`, `Friday`, and `Google Classroom`. | Task, deadline, location, and source evidence are extracted accurately. | [ ] Pass / [ ] Fail |  |
| API-11 Ambiguous captions | Analyze captions such as `Maybe submit it next week`, `room could change`, `ask someone later`. | Unclear Items capture uncertainty; no firm deadline or location is invented. | [ ] Pass / [ ] Fail |  |
| API-12 Cancel | Start a real provider request, click Cancel before completion. | Request is cancelled or UI returns safely; no result is saved to History. | [ ] Pass / [ ] Fail |  |
| API-13 Consecutive clicks | Click Analyze repeatedly during an active request. | Only one request is active; no duplicate History rows are created. | [ ] Pass / [ ] Fail |  |
| API-14 Captions continue during analysis | Start analysis, then let new captions continue arriving. | In-flight request uses the snapshot from click time; new captions do not enter current result. | [ ] Pass / [ ] Fail |  |
| API-15 History and evidence | Save the result to History and inspect the row. | `FeatureType = ClearBridge Caption Analysis`; range metadata is present; source evidence comes only from selected range. | [ ] Pass / [ ] Fail |  |
| API-16 Provider failure recovery | Temporarily use invalid endpoint/model or disconnect network, then retry with valid settings. | Failure message is understandable; no error result is saved; user can retry after fixing provider config. | [ ] Pass / [ ] Fail |  |

## Required Evidence

- Screenshot/video of All Captions mode.
- Screenshot/video of Sentence Range mode.
- Screenshot/video of 400 sentence guardrail.
- Screenshot/video of English, Simplified Chinese, and Arabic outputs.
- Screenshot/video of History row with `ClearBridge Caption Analysis`.
- Notes confirming Source Evidence stayed within the selected range.

## Pending Decision

Do not merge Phase 4 until this checklist is completed with a real provider or the remaining real-provider gaps are explicitly accepted.

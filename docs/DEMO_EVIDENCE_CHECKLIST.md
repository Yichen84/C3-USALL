# Demo Evidence Checklist

Use this checklist when collecting screenshots, video clips, and final submission proof.

## Interface Evidence

- [ ] ClearBridge input page.
- [ ] Mock Mode label visible.
- [x] English output.
- [x] Simplified Chinese output.
- [x] Arabic output.
- [ ] Action Checklist with checkboxes.
- [ ] Unclear Items section.
- [ ] Source Evidence section.
- [ ] History save result visible in History.
- [ ] Error handling for empty input.
- [ ] Error handling for short input.
- [ ] API key missing error for OpenAI-compatible provider.
- [ ] Cancel action.
- [x] English UI.
- [x] Simplified Chinese UI.
- [x] Arabic UI with RTL layout.
- [x] UI language restart-required message.
- [ ] UI language and ClearBridge output language shown as independent controls.
- [x] Screen region OCR capture flow.
- [x] Image upload OCR flow.
- [x] Global Screen OCR hotkey (`Alt + V`) from another app.
- [x] Screen OCR hotkey setting, disable toggle, and conflict/invalid message.
- [x] Dark translucent OCR quick action card near the selected screen region.
- [x] OCR quick action card status line with OCR engine, character count, and Local/Cloud label.
- [x] OCR quick action card buttons: Translate, Summarize, Analyze with ClearBridge, Open Full Review, Retry OCR, Close.
- [x] OCR Review text editing before action.
- [x] OCR Translate result with no Priority or Checklist.
- [x] OCR Summary result with no structured action fields.
- [x] ClearBridge OCR result with action checklist and source evidence.
- [x] AI OCR confirmation dialog before image upload.
- [ ] Caption page `Analyze Captions with ClearBridge` entry.
- [ ] Caption analysis All Captions mode.
- [ ] Caption analysis Sentence Range mode with From/To fields.
- [ ] Caption range preview with total and selected sentence counts.
- [ ] 400-sentence limit message.
- [ ] ClearBridge Caption Analysis result.
- [ ] History row with `ClearBridge Caption Analysis`.
- [ ] Rolling Summary panel on Caption page.
- [ ] Rolling Summary default Off / Stopped state.
- [ ] Rolling Summary interval selector showing 60 / 90 / 120 seconds and default 90 seconds.
- [ ] Rolling Summary Start / Pause / Resume / Stop controls.
- [ ] Process Now demo without waiting 90 seconds.
- [ ] Rolling Summary floating overlay next to the realtime caption overlay.
- [ ] Rolling Summary overlay drag, resize, Topmost toggle, collapse, close/reopen.
- [ ] Rolling Summary overlay internal scroll with older batches preserved while new batches append.
- [ ] Rolling Summary AI-generated / Unreviewed status.
- [ ] Rolling Summary temporary context privacy note.
- [ ] Save Confirmed Summary and History row with `ClearBridge Rolling Summary`.
- [ ] Clear Temporary Context behavior.
- [ ] Long-result demo uses the right-side scrollbar.
- [ ] Known mouse wheel limitation is disclosed if showing long results.

## Engineering Evidence

- [ ] Git commit timeline.
- [x] GitHub Actions passing.
- [ ] Open-source attribution visible.
- [ ] AI and data disclosure visible.
- [x] Phase 1 text test baseline documented in `docs/PHASE1_TEST_REPORT.md`.
- [x] Phase 2 language test report documented in `docs/PHASE2_LANGUAGE_TEST_REPORT.md`.
- [x] No `bin`, `obj`, `.vs`, API key, `setting.json`, `translation_history.db`, logs, or personal files committed.

## Current Capture Readiness

- ClearBridge page: implemented and passed manual regression; capture for final submission media.
- Mock Mode: service smoke passed and manual regression completed; capture for final submission media.
- Chinese output: manual regression passed; capture for final submission media.
- English output: manual regression passed; capture for final submission media.
- Arabic output: manual regression passed; capture for final submission media.
- Arabic UI / RTL: manual regression passed; capture screenshot/video evidence for final submission media.
- UI/output independence: implemented with separate Settings UI language and ClearBridge output language controls; capture one mixed-language scenario.
- UI language changes: intentionally apply after restart; manual regression passed, capture the restart-required message instead of demonstrating hot-switching.
- History save: service smoke passed through compatibility History write; History page switching passed manual regression.
- Text test case library: read and converted into Phase 1 baseline report.
- TC-01 Mock English/Chinese: service smoke passed; capture UI evidence when recording demo.
- TC-02 through TC-10: pending OpenAI-compatible provider/manual QA; do not claim as passed from Mock.
- No committed sensitive files: source scan completed with no key-pattern matches.
- GitHub Actions: PR #4 run passed before closeout; verify final main/tag status before release packaging.
- ClearBridge result content: viewable with wrapping and responsive layout; capture using the right-side scrollbar for long outputs.
- Mouse wheel over some result areas: accepted Phase 1 known issue; do not record or describe it as fully fixed.
- Fixed local test package path: `D:\USALL\USALL-Git\test-build\ClearBridge-Latest\LiveCaptionsTranslator.exe`.
- Phase 3 OCR Review: manually validated; capture screen region, upload image, editable extracted text, and three action buttons.
- Phase 3 input modes: implemented; capture Text mode separately from OCR modes to show that duplicate Notice Text and OCR Review controls no longer appear together.
- Screen OCR hotkey: manually validated with current default `Alt + V`; capture from another app during demo.
- Screen OCR hotkey settings: implemented; capture enabled/disabled state, changed shortcut, and invalid/conflict feedback if available.
- OCR quick action card: manually validated as a compact dark translucent window after `Alt + V` capture and Local OCR; capture the card near the selected screen area.
- Quick card versus Full Review: capture that the card offers fast Translate/Summarize/ClearBridge actions, while Open Full Review shows editable OCR text and full image preview.
- Quick card confirmation rule: capture or narrate that unclear/too-short OCR text is sent to Full Review before ClearBridge action analysis.
- Image upload: manually validated as a first-level OCR input button; capture PNG/JPG/JPEG/BMP upload path with file/source metadata and preview.
- OCR Translate: manually validated; capture that translation result is separate from ClearBridge fields.
- OCR Summary: manually validated; capture that summary result is separate from ClearBridge fields.
- ClearBridge OCR: manually validated; capture action analysis after reviewing OCR text.
- AI OCR confirmation: manually validated; capture confirmation dialog before any cloud upload.
- Windows Local OCR: implemented through Windows OCR API; capture accuracy examples after manual QA.
- DPI and multi-monitor: quick card placement clamps to the selected monitor working area by code. Not fully physically validated on all display configurations.
- Phase 4 Caption Analysis: implemented in the feature branch; capture Caption page entry, All/Range selector, selected preview, Analyze button, and manual Save to History after final manual QA.
- Caption source evidence: capture that evidence snippets come from selected caption text.
- Caption 400-sentence guardrail: capture a range or all-captions state above 400 showing the localized limit message.
- Caption History: capture `ClearBridge Caption Analysis` as a separate History feature type after saving.
- Caption auto-processing guardrail: narrate that captions are not automatically sent to providers; the user chooses scope and clicks Analyze.
- Phase 4 no-API audit: harness passed for range math, 400/401 boundaries, snapshot immutability, conservative deduplication, Mock language outputs, parser errors, and cancellation.
- Phase 4 real API evidence: runner passed configured-provider validation for 5-25 range, 120 all, Arabic output, 400 sentences, 401 local block, no-action, ambiguous content, Cancel, network error, and invalid model. Desktop History Save evidence is still pending.
- Phase 4 display validation: code-level verified; physical desktop validation remains pending for special DPI, multi-monitor, and Arabic layout combinations.
- Phase 5 Rolling Summary: implemented on the Caption page with Start/Pause/Resume/Stop, Process Now, memory-only compressed context, Mock/OpenAI-compatible provider paths, and confirmed History save.
- Phase 5 Rolling Summary overlay: implemented as a dark translucent floating window with shared session controls, batch-by-batch display, internal scroll, optional Topmost, collapse, close/reopen, and saved position/size. Temporary content itself is still memory-only.
- Phase 5 harness: passed 15 checks, including three-batch cache evolution, superseded fact correction, consume-once behavior, tiny-batch blocking, cancellation rollback, provider failure rollback, concurrent request rejection, pause/stop guards, 10-batch bounds, Mock English/Chinese/Arabic, confirmed History metadata, source evidence current-batch-only, parser null fields, wrapped JSON extraction, and invalid JSON rejection.
- Phase 5 real API validation: passed with synthetic captions only. Three batches succeeded, correction handling passed, Simplified Chinese succeeded, Arabic succeeded after one invalid-JSON retry, and synthetic failure rollback passed.
- Phase 5 physical desktop validation: still pending for live-caption streams, overlay placement/resize/collapse recording, and History UI inspection.

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
- [ ] Screen region OCR capture flow.
- [ ] Image upload OCR flow.
- [ ] Global Screen OCR hotkey (`Alt + V`) from another app.
- [ ] Screen OCR hotkey setting, disable toggle, and conflict/invalid message.
- [ ] Dark translucent OCR quick action card near the selected screen region.
- [ ] OCR quick action card status line with OCR engine, character count, and Local/Cloud label.
- [ ] OCR quick action card buttons: Translate, Summarize, Analyze with ClearBridge, Open Full Review, Retry OCR, Close.
- [ ] OCR Review text editing before action.
- [ ] OCR Translate result with no Priority or Checklist.
- [ ] OCR Summary result with no structured action fields.
- [ ] ClearBridge OCR result with action checklist and source evidence.
- [ ] AI OCR confirmation dialog before image upload.
- [ ] Long-result demo uses the right-side scrollbar.
- [ ] Known mouse wheel limitation is disclosed if showing long results.

## Engineering Evidence

- [ ] Git commit timeline.
- [ ] GitHub Actions passing.
- [ ] Open-source attribution visible.
- [ ] AI and data disclosure visible.
- [x] Phase 1 text test baseline documented in `docs/PHASE1_TEST_REPORT.md`.
- [x] Phase 2 language test report documented in `docs/PHASE2_LANGUAGE_TEST_REPORT.md`.
- [ ] No `bin`, `obj`, `.vs`, API key, `setting.json`, `translation_history.db`, logs, or personal files committed.

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
- GitHub Actions: verify after final PR merge to main.
- ClearBridge result content: viewable with wrapping and responsive layout; capture using the right-side scrollbar for long outputs.
- Mouse wheel over some result areas: accepted Phase 1 known issue; do not record or describe it as fully fixed.
- Fixed local test package path: `D:\USALL\USALL-Git\test-build\ClearBridge-Latest\LiveCaptionsTranslator.exe`.
- Phase 3 OCR Review: implemented; capture screen region, upload image, editable extracted text, and three action buttons.
- Phase 3 input modes: implemented; capture Text mode separately from OCR modes to show that duplicate Notice Text and OCR Review controls no longer appear together.
- Screen OCR hotkey: implemented with current default `Alt + V`; capture from browser, PDF reader, or chat software during manual desktop QA.
- Screen OCR hotkey settings: implemented; capture enabled/disabled state, changed shortcut, and invalid/conflict feedback if available.
- OCR quick action card: implemented as a compact dark translucent window after `Alt + V` capture and Local OCR; capture the card near the selected screen area.
- Quick card versus Full Review: capture that the card offers fast Translate/Summarize/ClearBridge actions, while Open Full Review shows editable OCR text and full image preview.
- Quick card confirmation rule: capture or narrate that unclear/too-short OCR text is sent to Full Review before ClearBridge action analysis.
- Image upload: implemented as a first-level OCR input button; capture PNG/JPG/JPEG/BMP upload path with file/source metadata and preview.
- OCR Translate: implemented; capture that translation result is separate from ClearBridge fields.
- OCR Summary: implemented; capture that summary result is separate from ClearBridge fields.
- ClearBridge OCR: implemented; capture action analysis after reviewing OCR text.
- AI OCR confirmation: implemented; capture confirmation dialog before any cloud upload.
- Windows Local OCR: implemented through Windows OCR API; capture accuracy examples after manual QA.
- DPI and multi-monitor: quick card placement clamps to the selected monitor working area by code; still collect 100%/125%/150% DPI, secondary monitor, and negative-coordinate monitor evidence manually.

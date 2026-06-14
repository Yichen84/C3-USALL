# Demo Evidence Checklist

Use this checklist when collecting screenshots, video clips, and final submission proof.

## Interface Evidence

- [ ] ClearBridge input page.
- [ ] Mock Mode label visible.
- [ ] English output.
- [ ] Simplified Chinese output.
- [ ] Action Checklist with checkboxes.
- [ ] Unclear Items section.
- [ ] Source Evidence section.
- [ ] History save result visible in History.
- [ ] Error handling for empty input.
- [ ] Error handling for short input.
- [ ] API key missing error for OpenAI-compatible provider.
- [ ] Cancel action.
- [ ] English UI.
- [ ] Simplified Chinese UI.
- [ ] Long-result demo uses the right-side scrollbar.
- [ ] Known mouse wheel limitation is disclosed if showing long results.

## Engineering Evidence

- [ ] Git commit timeline.
- [ ] GitHub Actions passing.
- [ ] Open-source attribution visible.
- [ ] AI and data disclosure visible.
- [x] Phase 1 text test baseline documented in `docs/PHASE1_TEST_REPORT.md`.
- [ ] No `bin`, `obj`, `.vs`, API key, `setting.json`, `translation_history.db`, logs, or personal files committed.

## Current Capture Readiness

- ClearBridge page: implemented; capture after desktop UI click-through.
- Mock Mode: service smoke passed; capture after desktop UI click-through.
- Chinese output: service smoke passed; capture after desktop UI click-through.
- English output: service smoke passed; capture after desktop UI click-through.
- History save: service smoke passed through compatibility History write; capture after desktop UI click-through.
- Text test case library: read and converted into Phase 1 baseline report.
- TC-01 Mock English/Chinese: service smoke passed; capture UI evidence when recording demo.
- TC-02 through TC-10: pending OpenAI-compatible provider/manual QA; do not claim as passed from Mock.
- No committed sensitive files: source scan completed with no key-pattern matches.
- GitHub Actions: pending after branch push.
- ClearBridge result content: viewable with wrapping and responsive layout; capture using the right-side scrollbar for long outputs.
- Mouse wheel over some result areas: accepted Phase 1 known issue; do not record or describe it as fully fixed.
- Fixed local test package path: `D:\USALL\USALL-Git\test-build\ClearBridge-Latest\LiveCaptionsTranslator.exe`.

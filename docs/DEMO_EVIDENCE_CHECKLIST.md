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

## Engineering Evidence

- [ ] Git commit timeline.
- [ ] GitHub Actions passing.
- [ ] Open-source attribution visible.
- [ ] AI and data disclosure visible.
- [ ] No `bin`, `obj`, `.vs`, API key, `setting.json`, `translation_history.db`, logs, or personal files committed.

## Current Capture Readiness

- ClearBridge page: implemented; capture after desktop UI click-through.
- Mock Mode: service smoke passed; capture after desktop UI click-through.
- Chinese output: service smoke passed; capture after desktop UI click-through.
- English output: service smoke passed; capture after desktop UI click-through.
- History save: service smoke passed through compatibility History write; capture after desktop UI click-through.
- No committed sensitive files: source scan completed with no key-pattern matches.
- GitHub Actions: pending after branch push.

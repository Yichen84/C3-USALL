# Submission Draft

## Project Name

ClearBridge for USALL 2026

## One-line Pitch

ClearBridge turns confusing notices into simple, source-backed action plans for students, parents, and families.

## Problem

Important school, government, medical, and community notices can be long, stressful, and hard to act on, especially for international families or people reading in a second language.

## Target Users

- Students.
- Parents and guardians.
- International families.
- English-language learners.
- People who need help understanding school, government, medical, or community instructions.

## Solution

ClearBridge converts pasted notice text into:
- Simple summary.
- Priority.
- Important points.
- Action checklist.
- Deadlines.
- Locations.
- Required documents.
- Warnings.
- Unclear items.
- Source evidence.

Phase 3 adds image and screen input. After OCR, users review and edit the extracted text, then choose ordinary translation, a plain summary, or ClearBridge structured action analysis.

Phase 4 adds manual caption-range analysis. After a live caption session, users can analyze all captions or only a selected sentence range, up to 400 sentences, and turn spoken content into a source-backed action plan.

## How It Works

1. User pastes a notice, captures a screen region, uploads an image, or collects real-time captions.
2. For image/screen input, Local OCR extracts text on device by default.
3. User reviews and edits OCR text before any follow-up action.
4. User chooses Translate, Summarize, or ClearBridge Analyze.
5. For captions, user chooses All Captions or a sentence range before clicking Analyze.
6. For longer sessions, user can start Rolling Summary, which processes new caption batches with temporary compressed context.
7. ClearBridge structured analysis returns a source-backed action plan.
8. User can copy the summary or action plan.
9. Result is saved to local History with feature type metadata only after the relevant user action.
10. User can choose the app UI language between English, Simplified Chinese, and Arabic; UI language changes are applied after restart.
11. UI language remains independent from the analysis output language.

## AI Use

- Optional OpenAI-compatible runtime provider for structured analysis.
- Optional OpenAI-compatible runtime provider for AI OCR after explicit user confirmation.
- Optional OpenAI-compatible runtime provider for plain summary after the user clicks Summarize.
- Optional OpenAI-compatible runtime provider for user-selected caption range analysis.
- Optional OpenAI-compatible runtime provider for user-enabled rolling caption summary.
- Fixed Mock Provider for demos, no-key use, CI stability, and network fallback.
- Codex assisted with implementation and documentation.

## Responsible AI

- The prompt instructs the model not to invent missing facts.
- Unclear information is placed in `unclear_items`.
- Important claims should include source evidence.
- Medical, legal, government eligibility, and safety topics do not replace professional advice.
- Mock Mode is clearly labeled.
- OCR stops at editable review text and does not automatically translate, summarize, or analyze.
- Caption analysis is manual: users choose all captions or a range and explicitly click Analyze.
- Rolling Summary is default-off, user-started, and processes batches rather than every caption line.
- Rolling context is temporary memory-only by default and is cleared when the app closes.
- Caption analysis is limited to 400 selected sentences and never silently truncates.
- Caption results are AI-generated and should be reviewed before saving.
- Rolling Summary results are AI-generated and unreviewed until the user saves a confirmed summary.
- AI OCR requires explicit confirmation before sending images to a cloud provider.
- Raw images are not saved to History.

## Technical Stack

- WPF / .NET 8.
- WPF-UI.
- SQLite via Microsoft.Data.Sqlite.
- Existing LiveCaptions Translator codebase.
- Optional OpenAI-compatible chat completions API.
- Windows OCR API for local OCR.
- Windows Forms overlay for one-time screen region selection.
- Current-session caption buffer and range selector for manual ClearBridge caption analysis.
- Rolling Summary session service with compressed in-memory context cache.

## Open-source Attribution

Built on top of LiveCaptions Translator by SakiRinn and contributors. The upstream license remains in `LICENSE`.

## What Was Built During the Hackathon

- ClearBridge structured analysis models.
- Mock and OpenAI-compatible ClearBridge providers.
- ClearBridge text analysis page.
- Navigation entry.
- ClearBridge History storage.
- English and Simplified Chinese ClearBridge UI strings.
- App-wide English, Simplified Chinese, and Arabic UI localization.
- Arabic RTL UI support.
- ClearBridge output in English, Simplified Chinese, and Arabic.
- One-time screen region capture for OCR review.
- Image upload for OCR review.
- Local Windows OCR and optional AI OCR.
- OCR Review with three independent actions: Translate, Summarize, and ClearBridge Analyze.
- OCR History classifications for OCR Translation, OCR Summary, and ClearBridge OCR.
- Manual ClearBridge caption analysis with All Captions and Sentence Range scopes.
- 400-sentence guardrail, immutable caption snapshot, conservative duplicate handling, and `ClearBridge Caption Analysis` History classification.
- Rolling Summary for captions with 60/90/120 second batch controls, Process Now, compressed temporary context, and `ClearBridge Rolling Summary` confirmed History classification.
- Restart-required UI language selection for stable English, Simplified Chinese, and Arabic startup.
- Hackathon documentation and disclosure files.

## Challenges

- Keeping the new ClearBridge flow separate from the existing translation logic.
- Adding History support without risky migration of existing user history.
- Supporting no-key demos honestly through Mock Mode.
- Avoiding unstable runtime UI hot-switching in WPF while still supporting persisted multilingual startup.
- Separating OCR providers from post-OCR text providers so ClearBridge does not replace ordinary translation or summary.
- Keeping image handling private by default while still supporting optional AI OCR.
- Adding caption analysis without rewriting the live caption capture pipeline or automatically sending speech-derived text to a provider.
- Adding rolling summary while keeping it default-off, non-concurrent, and memory-only by default.

## Accomplishments

- Phase 1 creates the minimum closed loop from pasted text to action plan to History.
- Release build and service-level smoke tests passed for the Phase 1 implementation.

## Future Work

- PDF support.
- More complete OCR DPI and dual-monitor validation.
- Persisted caption-session browser beyond current-session analysis.
- More polished rolling summary item-level review controls and real API/desktop validation.
- OCR accuracy improvements for low-quality and Arabic images.
- More complete professional localization review.
- Reminder and calendar integrations with explicit user approval.
- Better automated UI tests.
- Expanded provider options.

## Team Contributions

To be completed by the team with real names, roles, and evidence. Do not invent contributions.

## Demo Script

1. Open ClearBridge from the left navigation.
2. Click Example Notice.
3. Choose Simplified Chinese output.
4. Analyze in Mock Mode.
5. Show Simple Summary, Action Checklist, Unclear Items, and Source Evidence.
6. Copy Action Plan.
7. Open History and show saved ClearBridge summary.
8. Show the UI language selector and the restart-required message.
9. Restart into Arabic UI if time allows.
10. Show that UI language and ClearBridge output language are independent, for example Arabic UI with English output.
11. Switch to Caption, collect or simulate captions, then click Analyze Captions with ClearBridge.
12. Show All Captions versus Sentence Range, the 400-sentence limit, preview, and a ClearBridge caption analysis result.
13. Start Rolling Summary, show the 90-second default interval, click Process Now for demo pacing, and show AI-generated / Unreviewed status.
14. Save Confirmed Summary and show `ClearBridge Rolling Summary` in History.

## Repository Link

https://github.com/Yichen84/C3-USALL

## Video Link

Placeholder: add final demo video link.

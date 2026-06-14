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

## How It Works

1. User pastes a notice.
2. User chooses English or Simplified Chinese output.
3. User chooses Mock Mode or an OpenAI-compatible provider.
4. ClearBridge returns a structured action plan.
5. User can copy the summary or action plan.
6. Result is saved to local History.

## AI Use

- Optional OpenAI-compatible runtime provider for structured analysis.
- Fixed Mock Provider for demos, no-key use, CI stability, and network fallback.
- Codex assisted with implementation and documentation.

## Responsible AI

- The prompt instructs the model not to invent missing facts.
- Unclear information is placed in `unclear_items`.
- Important claims should include source evidence.
- Medical, legal, government eligibility, and safety topics do not replace professional advice.
- Mock Mode is clearly labeled.

## Technical Stack

- WPF / .NET 8.
- WPF-UI.
- SQLite via Microsoft.Data.Sqlite.
- Existing LiveCaptions Translator codebase.
- Optional OpenAI-compatible chat completions API.

## Open-source Attribution

Built on top of LiveCaptions Translator by SakiRinn and contributors. The upstream license remains in `LICENSE`.

## What Was Built During the Hackathon

- ClearBridge structured analysis models.
- Mock and OpenAI-compatible ClearBridge providers.
- ClearBridge text analysis page.
- Navigation entry.
- ClearBridge History storage.
- English and Simplified Chinese ClearBridge UI strings.
- Hackathon documentation and disclosure files.

## Challenges

- Keeping the new ClearBridge flow separate from the existing translation logic.
- Adding History support without risky migration of existing user history.
- Supporting no-key demos honestly through Mock Mode.

## Accomplishments

- Phase 1 creates the minimum closed loop from pasted text to action plan to History.
- Release build and service-level smoke tests passed for the Phase 1 implementation.

## Future Work

- OCR input.
- PDF support.
- Image upload.
- More output languages.
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
8. Switch UI language between English and Simplified Chinese.

## Repository Link

https://github.com/Yichen84/C3-USALL

## Video Link

Placeholder: add final demo video link.

# Competition Changes

This document separates upstream functionality from hackathon work. Do not claim upstream or pre-competition work as a hackathon-built feature.

## Upstream Existing Features

- Real-time translation of Windows Live Captions text.
- Caption display and overlay window.
- Translation history for caption translations.
- Multiple translation provider integrations already present in the upstream codebase.
- Settings UI for translation provider configuration, target language, context behavior, and overlay behavior.
- Build and release workflow inherited from the upstream project.

## Pre-Competition Reference Knowledge

- The team may have prior reference notes or experiments about C3-style crisis-to-action workflows. Those should be documented with dates and evidence before being cited in final submissions.
- No pre-competition implementation is claimed here as hackathon-built code.

## Features Built During the Hackathon

### ClearBridge Text Action Analysis

- Start date: 2026-06-14
- Completion date: 2026-06-14
- Corresponding commits:
  - `3c065cf` - `feat(clearbridge): add structured analysis services`
  - `97cc237` - `feat(clearbridge): add text analysis page`
- Corresponding files:
  - `src/models/ClearBridge/*`
  - `src/services/ClearBridge/*`
  - `src/pages/ClearBridgePage.xaml`
  - `src/pages/ClearBridgePage.xaml.cs`
  - `src/utils/HistoryLogger.cs`
  - `src/windows/MainWindow.xaml`
- User value:
  - Helps users convert confusing notices into simple summaries, important points, action steps, deadlines, locations, required documents, unclear items, warnings, and source evidence.
- AI-assisted development:
  - Yes. Codex assisted with implementation, verification, and documentation.

### ClearBridge Mock Provider

- Start date: 2026-06-14
- Completion date: 2026-06-14
- Corresponding commits:
  - `3c065cf` - `feat(clearbridge): add structured analysis services`
  - `97cc237` - `feat(clearbridge): add text analysis page`
- Corresponding files:
  - `src/services/ClearBridge/MockCrisisActionAnalysisProvider.cs`
  - `src/pages/ClearBridgePage.xaml.cs`
- User value:
  - Enables stable demos without API keys and makes the feature explainable when network access is unavailable.
- AI-assisted development:
  - Yes. Codex assisted with implementation.

### ClearBridge OpenAI-compatible Structured Analysis

- Start date: 2026-06-14
- Completion date: 2026-06-14
- Corresponding commits:
  - `3c065cf` - `feat(clearbridge): add structured analysis services`
- Corresponding files:
  - `src/services/ClearBridge/OpenAiCrisisActionAnalysisProvider.cs`
  - `src/services/ClearBridge/CrisisActionPromptBuilder.cs`
  - `src/services/ClearBridge/CrisisActionJsonParser.cs`
- User value:
  - Allows a configured OpenAI-compatible model to return structured action plans while enforcing JSON parsing and safe fallbacks.
- AI-assisted development:
  - Yes. Codex assisted with implementation.

### ClearBridge History and Evidence Trail

- Start date: 2026-06-14
- Completion date: 2026-06-14
- Corresponding commits:
  - `3c065cf` - `feat(clearbridge): add structured analysis services`
- Corresponding files:
  - `src/utils/HistoryLogger.cs`
- User value:
  - Preserves analysis results for review without replacing the existing History UI.
- AI-assisted development:
  - Yes. Codex assisted with implementation.

### Local Data Protection for ClearBridge Phase 1

- Start date: 2026-06-14
- Completion date: 2026-06-14
- Corresponding commits:
  - `fe39257` - `chore(clearbridge): protect local data and format whitespace`
- Corresponding files:
  - `.gitignore`
- User value:
  - Reduces the risk of committing local settings, API configuration files, SQLite history databases, or logs during hackathon development.
- AI-assisted development:
  - Yes. Codex identified and added the repository ignore rules.

### Hackathon Documentation and Disclosure

- Start date: 2026-06-14
- Completion date: 2026-06-14
- Corresponding commits:
  - pending - `docs(hackathon): document Phase 1 implementation`
- Corresponding files:
  - `docs/HACKATHON_BUILD_LOG.md`
  - `docs/COMPETITION_CHANGES.md`
  - `docs/AI_AND_DATA_DISCLOSURE.md`
  - `docs/SUBMISSION_DRAFT.md`
  - `docs/DEMO_EVIDENCE_CHECKLIST.md`
  - `docs/TEAM_CONTRIBUTIONS.md`
- User value:
  - Keeps submission evidence, AI disclosure, source attribution, testing status, and team contribution placeholders aligned with the actual Git history.
- AI-assisted development:
  - Yes. Codex drafted the initial documentation based on this repository state and verification results.

# AI and Data Disclosure

## AI Development Tools

### OpenAI Codex

Used for:
- Requirements interpretation.
- Repository inspection.
- Code generation.
- Debugging and build verification.
- Documentation drafting.
- Test planning.

### ChatGPT

Potential team use:
- Product planning.
- Pitch language.
- Technical advice.
- Demo script drafting.

Team members should add exact ChatGPT usage here if used outside this Codex session.

### Other Code or Design AI

None recorded for Phase 1. Add tools here if the team uses them later.

## Runtime AI Services

### OpenAI-compatible API

- Optional runtime provider for ClearBridge structured analysis.
- Uses existing user-configured OpenAI-compatible settings.
- API key is read from local settings and must not be committed.
- Prompts ask for strict JSON and source-backed claims.

### Google Translate

- Present in the upstream app for translation features.
- Not part of the new ClearBridge Phase 1 structured action analysis path.

### Mock Provider

- Local fixed-output provider.
- Uses the built-in school weather notice sample.
- Must be labeled as Mock Mode.
- Must not be described as real AI output.

## Data Sources

- User-pasted notices or messages.
- Built-in demonstration sample:
  - "Due to extreme weather conditions, all outdoor extracurricular activities scheduled after 12:30 PM today are suspended..."
- No public dataset is used in Phase 1.
- No automatic web lookup is performed.

## Privacy

- API keys must not be committed.
- `.gitignore` excludes local `setting.json`, `translation_history.db`, and logs.
- Authorization headers must not be logged.
- ClearBridge diagnostic logging records only provider, operation status, latency, input length, output length, and error type.
- Logs must not store full pasted source text or full AI response text.
- History stores the pasted source text and generated analysis result locally in SQLite because the user explicitly requested History integration.
- Mock data is clearly marked as Mock Mode.

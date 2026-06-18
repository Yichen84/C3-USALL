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

None recorded for Phase 1 or Phase 2. Add tools here if the team uses them later.

## Runtime AI Services

### OpenAI-compatible API

- Optional runtime provider for ClearBridge structured analysis.
- Optional runtime provider for Phase 3 AI OCR when the user explicitly chooses AI OCR and confirms image upload.
- Optional runtime provider for Phase 3 plain OCR summary when the user clicks Summarize.
- Optional runtime provider for Phase 4 manual caption range analysis after the user chooses a caption scope and clicks Analyze.
- Optional runtime provider for Phase 5 rolling caption summary after the user explicitly starts Rolling Summary or clicks Process Now.
- Uses existing user-configured OpenAI-compatible settings.
- API key is read from local settings and must not be committed.
- Prompts ask for strict JSON and source-backed claims.
- Phase 2 asks the configured model to return analysis fields in the selected competition output language: English, Simplified Chinese, or Arabic.
- Phase 4 uses a caption-specific prompt that warns the model about speech recognition errors, repeated fragments, incomplete sentences, and speaker examples.
- Phase 5 uses a rolling-summary prompt with compressed prior context plus only the current caption batch.
- `source_evidence.source_text` is instructed to preserve exact source wording instead of translating evidence snippets.

### Google Translate

- Present in the upstream app for translation features.
- Not part of the new ClearBridge Phase 1 structured action analysis path.
- May be used as an ordinary OCR Translation provider if selected by the user and configured in the app.

### Windows OCR API

- Used in Phase 3 for Local OCR through `Windows.Media.Ocr`.
- Runs on the user's Windows device and does not upload the image.
- No new third-party OCR NuGet package is added.
- Accuracy depends on Windows OCR language support and image quality.

### AI OCR

- Uses the configured OpenAI-compatible API only after the user clicks Retry with AI OCR and confirms upload.
- Prompt asks only for text extraction, not summary, translation, or ClearBridge analysis.
- Images may contain private information and are subject to the selected provider's policies.

### Mock Provider

- Local fixed-output provider.
- Uses the built-in school weather notice sample.
- Must be labeled as Mock Mode.
- Must not be described as real AI output.
- Phase 2 keeps fixed Mock outputs for the competition-visible languages, English, Simplified Chinese, and Arabic, for the same built-in sample.
- Phase 4 adds a fixed Mock Caption Analysis provider for no-key caption demos and fallback behavior.
- Phase 5 adds a fixed Mock Rolling Summary provider for no-key demos, cache-evolution validation, and harness testing.

## Data Sources

- User-pasted notices or messages.
- User-captured screen regions for Phase 3 OCR.
- User-uploaded image files for Phase 3 OCR.
- User-selected real-time caption ranges for Phase 4 manual caption analysis.
- User-enabled rolling caption batches for Phase 5 rolling summary.
- Built-in demonstration sample:
  - "Due to extreme weather conditions, all outdoor extracurricular activities scheduled after 12:30 PM today are suspended..."
- No public dataset is used in Phase 1.
- No public dataset is used in Phase 2.
- No public dataset is used in Phase 3.
- No public dataset is used in Phase 4.
- No public dataset is used in Phase 5.
- No automatic web lookup is performed.

## Privacy

- API keys must not be committed.
- `.gitignore` excludes local `setting.json`, `translation_history.db`, and logs.
- Authorization headers must not be logged.
- ClearBridge diagnostic logging records only provider, operation status, latency, input length, output length, and error type.
- Logs must not store full pasted source text or full AI response text.
- History stores the pasted source text and generated analysis result locally in SQLite because the user explicitly requested History integration.
- Phase 3 History stores confirmed OCR text and OCR metadata for OCR Translation, OCR Summary, and ClearBridge OCR records.
- Phase 3 History does not store raw screenshots, uploaded image files, or Base64 image content.
- Phase 4 History stores only the selected caption range used for analysis, not captions outside the user's selected scope.
- Phase 5 keeps raw rolling batches and compressed context in memory by default and clears them on app close or Clear Temporary Context.
- Phase 5 History stores only user-confirmed rolling summary output and metadata; it does not save full raw caption batches by default.
- Phase 4 diagnostic logging must not store the full caption transcript or full model response.
- AI OCR requires explicit confirmation before any image is sent to a cloud provider.
- Mock data is clearly marked as Mock Mode.
- UI language preference is stored locally in `setting.json` as `UiLanguage`.
- UI language changes are saved locally and applied after restarting the application.
- Localization JSON files contain only app UI strings and no user data.

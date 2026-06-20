# ClearBridge P1 Output Language Enforcement Fix

Date: 2026-06-20

Branch: `fix/clearbridge-output-language-enforcement`

## Issue

Manual testing found that ClearBridge Text Analysis sometimes returned structured values in the input language even when the selected output language was Simplified Chinese. The failing scenario was Arabic input, `zh-CN` / Simplified Chinese output, and the OpenAI-compatible provider.

Risk level: P1 because multilingual output reliability is a core ClearBridge claim.

## Root Cause

- The prompt asked the model to write in the selected language, but the language rule was not strict enough.
- The prompt did not consistently separate fixed English JSON keys from user-visible JSON string values.
- The provider accepted parsed JSON without checking that user-visible fields matched the selected output language.
- Existing retry behavior focused on JSON validity, not output-language compliance.

## Fix

- Added canonical output-language normalization for `English`, `Simplified Chinese`, and `Arabic`, including aliases such as `zh-CN`.
- Strengthened ClearBridge text, caption analysis, and rolling summary prompts with an explicit output language requirement.
- Required JSON property names to remain English snake_case.
- Required every user-visible JSON string value to use the selected output language, except `source_evidence.source_text`.
- Preserved `source_evidence.source_text` as original input text and excluded it from local language scoring.
- Added local lightweight language validation for structured ClearBridge and Rolling Summary results.
- Added one strict language retry for OpenAI-compatible structured analysis and Rolling Summary.
- If the retry still fails language validation, the provider raises `OutputLanguageMismatch`.
- Wrong-language results are not rendered as success and are not saved to History.

## Validation

Automated and harness validation:

- `dotnet restore .\LiveCaptionsTranslator.sln`: PASS.
- `dotnet format .\LiveCaptionsTranslator.csproj --verify-no-changes --verbosity minimal`: PASS.
- `dotnet build .\LiveCaptionsTranslator.sln -c Release --no-restore`: PASS with existing nullable warnings.
- `dotnet run --project .\tools\Phase4CaptionAudit\Phase4CaptionAudit.csproj -c Release`: PASS, 10 checks.
- `dotnet run --project .\tools\Phase5RollingSummaryAudit\Phase5RollingSummaryAudit.csproj -c Release`: PASS, 15 checks.
- `dotnet run --project .\tools\ClearBridgeOutputLanguageAudit\ClearBridgeOutputLanguageAudit.csproj -c Release`: PASS, 12 checks.
- `dotnet publish .\LiveCaptionsTranslator.csproj -c Release -r win-x64 --self-contained true`: PASS.

The new output-language audit covers:

- `zh-CN` maps to `Simplified Chinese` before prompting.
- Prompt rules distinguish English snake_case JSON keys from translated values.
- Language retry prompt preserves the selected target language.
- Chinese output passes when Arabic appears only in `source_text`.
- Arabic-visible values are rejected for Chinese output.
- English output passes when Chinese appears only in `source_text`.
- Chinese-visible values are rejected for English output.
- Arabic output passes when English appears only in `source_text`.
- Translated JSON keys are rejected after parsing defaults.
- Rolling Summary Chinese output passes.
- Rolling Summary wrong-language output is rejected.
- `source_evidence.source_text` is excluded from language scoring.

## Pending Manual Real-Provider Validation

No paid/external real-provider calls were made in this automated pass. The fixed local package is ready for manual API validation with synthetic text:

- Arabic to Simplified Chinese, 5 consecutive runs.
- English to Simplified Chinese, 3 consecutive runs.
- Simplified Chinese to English, 3 consecutive runs.
- English to Arabic, 3 consecutive runs.
- OCR to Simplified Chinese.
- Caption Analysis to Simplified Chinese.
- Rolling Summary to Simplified Chinese.

## Fixed Package

Path:

`D:\USALL\USALL-Git\test-build\ClearBridge-Latest\LiveCaptionsTranslator.exe`

The fixed package is for manual P1 validation only. The formal GitHub Release was not updated.

## Known Limits

- The language detector is intentionally lightweight and script-based; it avoids large dependencies.
- It checks aggregated user-visible fields and may allow short numeric or proper-noun-heavy content.
- It does not translate or rewrite a failed result locally; it asks the provider for one strict rewrite.
- Existing long-result mouse wheel limitations remain unchanged.

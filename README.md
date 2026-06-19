# ClearBridge

ClearBridge turns confusing multilingual notices, on-screen text, and live captions into clear, source-backed next steps.

ClearBridge is a Windows desktop project built on the open-source LiveCaptions Translator codebase. It is designed for multilingual students, parents, international families, and community users who need to understand stressful school, government, medical, or community information before deciding what to do next.

> AI-generated content may contain omissions or errors. Review the original source evidence and confirm important dates, locations, document requirements, warnings, and action steps before acting. ClearBridge does not automatically perform external actions for the user.
>
> AI 生成内容可能存在遗漏或错误。请查看原文证据，并在确认重要日期、地点、文件要求、警告和行动步骤后再采取行动。ClearBridge 不会自动替用户执行外部任务。

## About the Project

ClearBridge is a crisis-to-action assistant. Instead of only translating text, it helps users turn complicated or anxious information into:

- A simple summary.
- Important points.
- Action steps.
- Dates and deadlines.
- Locations.
- Required documents.
- Warnings.
- Unclear items.
- Source evidence copied from the original text.

The app keeps human review in the loop. Users choose what text to analyze, review OCR or caption text before sending it to a provider, and confirm results before saving or acting.

## Problem

Important notices are often long, technical, multilingual, or delivered through screenshots and live captions. People may miss deadlines, documents, or warnings because the message is hard to parse under pressure.

ClearBridge focuses on the practical question:

What does this message mean, what should I do, and what still needs to be confirmed?

## Solution

ClearBridge combines text analysis, OCR review, caption-range analysis, and rolling caption summaries into one user-controlled workflow:

1. Paste text, capture a screen region, upload an image, or collect live captions.
2. Review and correct the source text when needed.
3. Choose ordinary translation, plain summary, or ClearBridge action analysis.
4. Review source-backed results.
5. Save confirmed results to local History.

## Key Features

- Text ClearBridge analysis.
- English, Simplified Chinese, and Arabic interface.
- English, Simplified Chinese, and Arabic ClearBridge output.
- Local screen-region OCR.
- Optional cloud OCR with explicit user confirmation.
- OCR Translate, Summarize, and Analyze actions.
- Manual caption analysis with All Captions or Sentence Range.
- 400-sentence guardrail for caption analysis.
- Rolling Summary overlay for live captions.
- Source Evidence for major claims.
- Unclear Items for missing or ambiguous information.
- Human confirmation before saving important AI-generated results.
- History for confirmed results.

## AI Architecture

ClearBridge separates providers by role:

- OCR providers extract text from images or screen regions.
- Translation providers handle ordinary translation.
- Summary providers create plain summaries.
- ClearBridge providers create structured action analysis.
- Rolling Summary providers process live caption batches.

Mock providers are included for demos, tests, and no-key environments. Mock output is labeled and should not be described as real AI output.

OpenAI-compatible providers are optional and configured by the user. The release package does not include API keys or `setting.json`.

## Human-in-the-Loop

ClearBridge does not automatically submit forms, send email, create calendar events, contact organizations, or make decisions for the user. Users remain responsible for reviewing source evidence and confirming important details.

Examples of human control:

- Cloud OCR requires confirmation before upload.
- OCR text can be edited before Translate, Summarize, or ClearBridge Analyze.
- Caption analysis only runs after the user selects All Captions or a sentence range.
- Rolling Summary starts, pauses, resumes, stops, and saves only through user action.
- History saves confirmed results instead of raw private working buffers.

## Responsible AI

AI-generated content may contain omissions or errors. Review the original source evidence and confirm important dates, locations, document requirements, warnings, and action steps before acting. ClearBridge does not automatically perform external actions for the user.

Do not use ClearBridge as a substitute for professional medical, legal, immigration, financial, or government eligibility advice. When a notice affects rights, benefits, health, safety, money, or deadlines, verify the information with the original source or a qualified professional.

## Privacy

- Do not enter passwords, banking information, identity numbers, or other highly sensitive data unless you understand the provider and storage risks.
- Online AI providers receive the selected text that the user sends for analysis, summary, translation, or OCR.
- Cloud OCR sends the selected image to the configured provider only after user confirmation.
- Local OCR uses Windows on-device OCR when available.
- Rolling Summary temporary raw buffers and compressed context are memory-only by default.
- Temporary Rolling Summary context is cleared when the app exits.
- Release assets do not include user API keys, authorization headers, `setting.json`, user databases, logs, or raw user data.

## Supported Languages

Interface:

- English
- Simplified Chinese
- Arabic

ClearBridge structured output:

- English
- Simplified Chinese
- Arabic

Ordinary translation providers may support additional languages depending on user configuration.

## Demo and Download

Release page:

[https://github.com/Yichen84/C3-USALL/releases/tag/clearbridge-usall-final](https://github.com/Yichen84/C3-USALL/releases/tag/clearbridge-usall-final)

Recommended download:

`ClearBridge-USALL-Final-win-x64.zip`

Extract the entire ZIP before running `LiveCaptionsTranslator.exe`.

Platform:

- Windows 10/11 x64.
- The packaged Windows desktop app does not run directly on macOS, iPadOS, Android, or iOS.
- The release does not include user API keys or `setting.json`.

## Testing

The final build process included:

- Restore, format, release build, and publish checks.
- Phase 4 caption analysis audit.
- Phase 5 rolling summary audit.
- ZIP extraction smoke test.
- Single-file EXE smoke test.
- PDF/TXT quick-start guide validation.
- Security checks for settings, databases, logs, user data, and API key files.

Some physical scenarios, including every DPI and multi-monitor combination, were not fully tested.

## Known Limitations

- Some long ClearBridge result areas work better with the right-side scrollbar than the mouse wheel.
- Not every DPI and multi-monitor setup has been physically validated.
- Long real classroom caption streams have not been fully covered end to end.
- Arabic output from some providers may trigger one strict JSON retry before succeeding.
- Network and provider quality can affect response speed and consistency.
- A Google-related credential-like constant inherited from the upstream base is documented as an accepted upstream dependency risk. It is not a user personal key and not a ClearBridge/OpenAI-added key.

## Open-Source Attribution

ClearBridge is based on the open-source LiveCaptions Translator project.

- Upstream project: LiveCaptions Translator.
- Upstream author/organization: SakiRinn and contributors.
- Upstream repository: [https://github.com/SakiRinn/LiveCaptions-Translator](https://github.com/SakiRinn/LiveCaptions-Translator).
- Upstream license: Apache License 2.0.

The original project and applicable upstream components are licensed under the Apache License 2.0. Original copyright, license, and attribution notices are preserved where required.

The ClearBridge team added and modified substantial functionality for the USAII Global AI Hackathon 2026, including multilingual action-oriented analysis, OCR workflows, caption analysis, rolling summaries, human-in-the-loop confirmation, source-evidence validation, privacy safeguards, testing harnesses, and multilingual interface support.

ClearBridge is an independent derivative project and is not officially affiliated with or endorsed by the original upstream authors.

See [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt) for upstream and dependency notices.

## Team

ClearBridge / C3-USALL hackathon team.

Detailed individual contributions should be recorded only when confirmed by the team and supported by commits, documents, test reports, or demo evidence.

## License

This repository preserves the upstream Apache License 2.0 in [LICENSE](LICENSE).

Upstream code, applicable upstream components, and ClearBridge modifications are distributed under the Apache License 2.0 requirements unless a dependency states a separate license. Third-party dependency notices are listed in [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt).

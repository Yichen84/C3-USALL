# Final Build Manifest

## Final Package

- Directory: `D:\USALL\USALL-Git\final-package\ClearBridge-USALL-Final`
- EXE: `D:\USALL\USALL-Git\final-package\ClearBridge-USALL-Final\App\LiveCaptionsTranslator.exe`
- Functional baseline commit: `d7d5fe3429435c2f53b5b9a6323a9919883c98d1`
- Tag: `phase5-rolling-summary`
- Runtime: `win-x64 self-contained`
- Configuration: `Release`
- File count: `8`
- Total size: `196266593` bytes
- EXE SHA-256: `213512398f81c0b2cc1de84fff8f92db63c6b21303bbdcaa85aba1acc9ef1b9d`
- Checksums file: `D:\USALL\USALL-Git\final-package\ClearBridge-USALL-Final\Checksums\SHA256SUMS.txt`
- Main DLL: not present in the final App folder because the project publishes as a win-x64 self-contained single-file EXE.

## Included Package Documents

- `Docs\README_RUN.txt`
- `Docs\BUILD_INFO.txt`
- `Docs\KNOWN_ISSUES.txt`
- `Docs\PRIVACY_AND_AI_NOTICE.txt`
- `Docs\DEMO_QUICK_START.txt`
- `Checksums\SHA256SUMS.txt`

## Excluded From Package

- User personal API keys.
- OpenAI/ClearBridge provider API keys.
- `setting.json`.
- `translation_history.db`.
- Logs.
- Raw captions.
- Compressed rolling context.
- Git metadata.
- Note: an inherited upstream Google-related credential-like constant remains in the app binary and is documented as an accepted upstream dependency risk.

## Smoke Result

- Final package EXE launched successfully.
- App was closed after smoke.
- Generated `setting.json`, database, and logs were removed from the package directory after smoke.

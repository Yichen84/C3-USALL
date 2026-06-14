# Phase 1 Test Report - ClearBridge Text Case Baseline

Date: 2026-06-14

Source baseline: `C:\Users\81205\Desktop\ClearBridge_文本测试案例库.docx`

Branch: `feature/clearbridge-phase1`

## Scope

This report records the text-input test baseline for ClearBridge Phase 1. The full Word test library was read and converted into the case matrix below.

Phase 1 executes pasted-text cases first. Image/OCR cases are reserved for the OCR phase. Expanded multilingual UI/output cases are reserved for the language expansion phase. Real-time caption summary cases are reserved for the final phase.

Mock Provider is intentionally fixed to the school weather notice sample. It must not be used to fake passing unrelated cases. OpenAI-compatible bulk testing was not run in this pass because it requires a configured API key and explicit approval for real external API usage.

## Summary Table

| Case | Provider | Language | Result | Main Issue |
| --- | --- | --- | --- | --- |
| TC-01A | Mock | English | Pass | Matches the fixed Mock weather notice. |
| TC-01B | Mock | Simplified Chinese | Pass | Matches the fixed Mock weather notice and preserves `12:30 PM`. |
| TC-02 | OpenAI-compatible | English / Simplified Chinese | Not Run | Requires configured real provider; Mock would be invalid because it is fixed to TC-01. |
| TC-03 | OpenAI-compatible | English / Simplified Chinese | Not Run | Requires configured real provider; medical safety wording must be checked manually. |
| TC-04 | OpenAI-compatible | English / Simplified Chinese | Not Run | Requires configured real provider; relative deadline must not be converted to a fabricated date. |
| TC-05 | OpenAI-compatible | English / Simplified Chinese | Not Run | Requires configured real provider; eligibility must not be promised. |
| TC-06 | OpenAI-compatible | English / Simplified Chinese | Not Run | Requires configured real provider; missing details must be surfaced clearly. |
| TC-07 | OpenAI-compatible | English / Simplified Chinese | Not Run | Requires configured real provider; relative date ambiguity must be preserved. |
| TC-08 | OpenAI-compatible | English / Simplified Chinese | Not Run | Requires configured real provider; must distinguish action deadlines from interview contact date. |
| TC-09 | OpenAI-compatible | English | Not Run | Requires configured real provider; Chinese input to English output needs manual review. |
| TC-10 | OpenAI-compatible | English / Simplified Chinese | Not Run | Requires configured real provider; role-specific requirements must not be over-applied. |

## Detailed Case Records

### TC-01 - Extreme Weather Outdoor Activity Suspension

Input:

```text
Due to extreme weather conditions, all outdoor extracurricular activities scheduled after 12:30 PM today are suspended. Students should check with their activity coordinators for indoor alternatives. Bus schedules have not been confirmed to change. Parents should monitor the school portal for further updates.
```

Provider: Mock

Output languages executed: English, Simplified Chinese

Result: Pass

Checks:

- Summary accurate: Yes. It states that outdoor activities after `12:30 PM` are suspended.
- Actions executable: Yes. It tells students to check with activity coordinators and parents to monitor the school portal.
- Deadline / location correct: Yes. It preserves `12:30 PM today` and uses school portal / coordinator contact channel only from the source.
- Unclear Items: Yes. It identifies that bus schedule changes are not confirmed.
- Source Evidence: Yes. Evidence quotes come from the original notice.
- Fabrication: No. It does not claim buses are unchanged.
- Notes: This is the only content case that Mock Provider is allowed to pass because Mock is intentionally fixed to this sample.

### TC-02 - Parent Consent Form Deadline

Input:

```text
All Grade 11 students joining the university visit must submit the signed parent consent form to the counseling office by Thursday, September 17 at 3:00 PM. Students who do not submit the form by the deadline will not be allowed to attend. The notice does not state whether digital signatures are accepted.
```

Provider: OpenAI-compatible planned

Output languages planned: English, Simplified Chinese

Result: Not Run

Checks to perform:

- Summary should state Grade 11 university visit students must submit the signed parent consent form.
- Actions should include getting parent signature and submitting to the counseling office.
- Deadline / location should preserve `Thursday, September 17 at 3:00 PM` and `counseling office`.
- Unclear Items should include whether digital signatures are accepted.
- Source Evidence should quote the submission deadline sentence.
- Fabrication must not state that digital signatures are accepted.

### TC-03 - Hospital Discharge Instructions

Input:

```text
Take the prescribed antibiotic twice daily for seven days. Keep the wound dry for 48 hours. Contact the clinic immediately if you develop a fever above 38.5°C, increasing redness, severe pain, or discharge. A follow-up appointment is recommended within one week, but no appointment has been scheduled.
```

Provider: OpenAI-compatible planned

Output languages planned: English, Simplified Chinese

Result: Not Run

Checks to perform:

- Summary should cover medication, wound care, and warning symptoms.
- Actions should include taking antibiotics twice daily for seven days, keeping the wound dry for 48 hours, and arranging follow-up.
- Deadline / location should preserve `seven days`, `48 hours`, `within one week`, and `clinic`.
- Unclear Items should state that no specific follow-up appointment has been scheduled.
- Source Evidence should quote the clinic warning sentence.
- Fabrication must not add medical diagnosis or treatment advice.
- Warning should include a professional/clinic confirmation note because this is medical content.

### TC-04 - Housing Assistance Missing Documents

Input:

```text
Your housing assistance application is incomplete. Please provide proof of income for all adult household members and a copy of your current tenancy contract within 10 calendar days of the date of this letter. Documents may be submitted in person at the Housing Support Center or through the online portal. The letter does not list the portal URL.
```

Provider: OpenAI-compatible planned

Output languages planned: English, Simplified Chinese

Result: Not Run

Checks to perform:

- Summary should state that the housing assistance application is incomplete.
- Actions should include collecting income proof for all adult household members and current tenancy contract copy.
- Deadline / location should preserve `within 10 calendar days of the date of this letter`, `Housing Support Center`, and online portal.
- Unclear Items should include missing portal URL and missing letter date.
- Source Evidence should quote the `within 10 calendar days` sentence.
- Fabrication must not calculate an absolute deadline date or decide eligibility.

### TC-05 - Emergency Food Support Application

Input:

```text
Applicants for the emergency food support program must bring a government-issued ID, proof of address, and documents showing household income. Applications are accepted Monday to Wednesday from 9:00 AM to 1:00 PM at Community Center B. The notice does not explain whether an appointment is required.
```

Provider: OpenAI-compatible planned

Output languages planned: English, Simplified Chinese

Result: Not Run

Checks to perform:

- Summary should state required documents and application time/location.
- Actions should include preparing ID, proof of address, and household income documents.
- Deadline / location should preserve `Monday to Wednesday`, `9:00 AM to 1:00 PM`, and `Community Center B`.
- Unclear Items should include whether an appointment is required.
- Source Evidence should quote the required-document sentence.
- Fabrication must not promise that the user qualifies.

### TC-06 - Incomplete Leadership Workshop Notice

Input:

```text
Students selected for the leadership workshop should report to the designated meeting point after lunch and bring the required materials. Late arrivals may not be admitted.
```

Provider: OpenAI-compatible planned

Output languages planned: English, Simplified Chinese

Result: Not Run

Checks to perform:

- Summary should state that selected students need to report after lunch.
- Actions should include confirming meeting point, required materials, and arriving on time.
- Deadline / location should not invent a date, exact time, meeting point, or material list.
- Unclear Items should include missing date, meeting point, required materials, and exact meaning of `after lunch`.
- Source Evidence should quote the report/materials sentence.
- Fabrication must not invent location or materials.

### TC-07 - Ambiguous Payment Deadline

Input:

```text
Your outstanding activity fee should be paid by next Friday to avoid suspension from future trips. Payment can be made at the finance office. Contact the school if you have already paid.
```

Provider: OpenAI-compatible planned

Output languages planned: English, Simplified Chinese

Result: Not Run

Checks to perform:

- Summary should state that the activity fee should be paid by `next Friday`.
- Actions should include confirming notice date, paying at finance office if unpaid, and contacting school if already paid.
- Deadline / location should preserve `next Friday` and `finance office`.
- Unclear Items should state that the notice date is missing, so the exact calendar date cannot be determined.
- Source Evidence should quote `should be paid by next Friday`.
- Fabrication must not generate a specific calendar date.

### TC-08 - Government Notice With Multiple Deadlines

Input:

```text
Submit the registration form by 5:00 PM on October 3. Supporting documents may be uploaded until October 7. Applicants selected for interview will be contacted by October 12. No action is required if you have already completed registration and uploaded all documents.
```

Provider: OpenAI-compatible planned

Output languages planned: English, Simplified Chinese

Result: Not Run

Checks to perform:

- Summary should distinguish registration and supporting-document deadlines.
- Actions should include submitting registration form by `5:00 PM on October 3`, uploading supporting documents by `October 7`, and doing nothing if both are complete.
- Deadline / location should not treat `October 12` as a user submission deadline.
- Unclear Items should include missing interview contact method.
- Source Evidence should quote both deadline sentences.
- Fabrication must not create extra submission requirements.

### TC-09 - Chinese School Notice to English Action Plan

Input:

```text
因校内电力维护，明天上午第一、第二节课将改为线上授课。学生需在早上7:45前登录学校学习平台，并确保摄像头和麦克风可以正常使用。第三节课起恢复正常到校上课。通知未说明校车上午是否照常运行。
```

Provider: OpenAI-compatible planned

Output language planned: English

Result: Not Run

Checks to perform:

- Summary should state that the first two morning periods are online and in-person classes resume from the third period.
- Actions should include logging in by `7:45`, checking camera/microphone, and confirming third-period arrival.
- Deadline / location should preserve `7:45`, school learning platform, and third period.
- Unclear Items should include whether morning buses run normally.
- Source Evidence should quote the original Chinese sentence about first and second periods moving online.
- Fabrication must not invent a specific bus schedule.

### TC-10 - Long Text Key Action Extraction

Input:

```text
The school will hold its annual academic conference next month. The event includes student presentations, parent meetings, workshops, and campus tours. Families are welcome to attend. Students presenting research must upload their final slides by 6:00 PM on November 8 and report to the auditorium at 8:15 AM on November 10. Presenters should bring their school ID and a backup copy of the slides on a USB drive. General attendees do not need to upload any materials. Parking instructions will be shared later.
```

Provider: OpenAI-compatible planned

Output languages planned: English, Simplified Chinese

Result: Not Run

Checks to perform:

- Summary should state that research presenters have specific upload/reporting requirements and general attendees do not need to upload materials.
- Actions should include presenter slide upload by `6:00 PM on November 8`, reporting to the auditorium at `8:15 AM on November 10`, and bringing school ID plus USB backup.
- Deadline / location should preserve November 8, November 10, and auditorium.
- Unclear Items should include parking instructions will be shared later.
- Source Evidence should quote the `Students presenting research must upload...` sentence.
- Fabrication must not apply presenter requirements to all attendees.

## Error And Exception Baseline

The Word case library also defines error and exception tests. Current service-level smoke coverage:

| Test | Status | Evidence |
| --- | --- | --- |
| Empty input | Pass | `InputEmpty` validation raised. |
| Short input under 30 characters | Pass | `InputTooShort` validation raised. |
| Missing output language | Pass | `OutputLanguageMissing` validation raised. |
| Unknown provider | Pass | `ProviderNotConfigured` validation raised. |
| Invalid JSON | Pass | `InvalidJson` parser error raised without crashing. |
| Cancellation | Pass | `OperationCanceledException`; no completed result saved. |
| History compatibility save | Pass | ClearBridge summary visible through existing History table. |
| Overlong input | Not Run | Needs dedicated test input over 12,000 characters. |
| Missing API key | Not Run | Needs OpenAI-compatible provider selected with no local key. |
| Network timeout | Not Run | Needs controlled network/provider timeout. |
| History failure | Not Run | Needs controlled read-only/unwritable database setup. |
| Clipboard busy | Not Run | Needs desktop UI clipboard contention test. |

## Aggregate Result

- Text cases read from baseline: 10
- Text execution rows passed: 2
- Text execution rows failed: 0
- Text content cases pending real provider/manual review: 9
- Error/exception checks passed in service smoke: 7
- Error/exception checks pending: 5
- Fabrication found: None in executed Mock TC-01 checks.
- JSON/parsing issues found: None in smoke; invalid JSON produced the expected controlled error.
- UI issues found: None from service smoke; desktop UI click-through still required.

## Recommendation

Do not claim full Phase 1 case-library pass yet. TC-01 is validated through Mock in English and Simplified Chinese, and the remaining text cases are now established as the real-provider/manual acceptance baseline. Proceed to the next development step only after running TC-02 through TC-10 against a configured OpenAI-compatible provider or an approved manual QA session, and after completing desktop UI click-through evidence.

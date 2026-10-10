# Omskep phase 2 handoff: the SSML editor (Task A)

Phase 2 is complete and verified. This document follows the template in `workflow.md`.
Attach it, with the phase 0 and phase 1 handoffs, to the coordinator chat and to the phase 3 chat.

## Phase goal

Turn the main window from an "editor arrives" placeholder into a working document editor:
import a study PDF or pasted text into a well-formed SSML document, clean it in a line-numbered
editor that checks the markup live, save and reopen it without ever losing edits, and expose the
state phase 3 needs to gate export (document is well-formed, upper-bound billable character count).

## Files added

### `src/Omskep.Core/Import`

| File | Contents |
| --- | --- |
| `TextSanitizer.vb` | `TextSanitizer.Sanitize` (pure) and `SanitizeResult`. The one constant `ArticleQuote` (straight `'`) controls the Afrikaans article quote. |
| `ImportTypes.vb` | `ImportOutcome`, `ImportResult`, `ImportFlag`/`ImportFlagKind`, `IPdfPageReader`, `PdfPasswordRequiredException`. |
| `DocumentComposer.vb` | Wraps sanitized text in `<speak version xmlns xml:lang>` and one `<voice>`. |
| `ImportFlagFinder.vb` | Reports wide space runs and line-end hyphens in document coordinates. Report only. |
| `DocumentImporter.vb` | `ImportPdf` and `ImportText`: per-page sanitizing, empty-page handling, outcome and user message. Never throws for bad content. |
| `PdfPigPageReader.vb` | PdfPig adapter using `ContentOrderTextExtractor`. The only file that references PdfPig. |
| `PasteHandler.vb` | Decides what Paste and Paste as plain text do (replace blank document, insert at caret, or nothing). |

### `src/Omskep.Core/Documents`

| File | Contents |
| --- | --- |
| `WellFormednessChecker.vb` | `Check` returns a `CheckResult`: first error line/column, message, and the probable unclosed element. DTDs refused. Never throws. |
| `BillableCharacterCounter.vb` | `CountUpperBound` (everything except `<speak>`/`<voice>` tags, CRLF as one). |
| `DocumentModel.vb` | Save state, check state, revision guard so a stale check result is ignored. |
| `DocumentFile.vb` | Text open and save through `AtomicFile`; clear `DocumentFileException` messages. |
| `RecoveryStore.vb` | Per-instance autosave snapshots and orphan detection. |
| `DocumentScaffold.vb` | `IsBlank`, `BodyStartLine`, `ScaffoldDefaults.Choose` (default voice from settings). |
| `FileKinds.vb` | Classifies a file as SSML, PDF or text; `LooksLikeSsml`. |
| `HebrewOrder.vb` | `TextEdit`, `ReverseRuns` (visual to reading order for Hebrew from PDFs). |
| `CommandLineArguments.vb` | Finds the document path Windows passes on double-click or Open with. |
| `TextSearch.vb` | Find, Replace and Replace All (literal or regex) as pure functions returning edits. |

### `src/Omskep.Core/Session`

- `DocumentFormState.vb`: menu enables, the export gate (`ExportEnabled`, `ExportBlockedReason`), window title, status texts.

### `src/Omskep.App`

- `Editor/SsmlEditor.vb`: the only code that touches Scintilla5.NET. Narrow API (see below).
- `FindReplaceForm.vb`: the Find and Replace window (built in code, no designer file).

### Tests (`tests/Omskep.Core.Tests`)

`Import/TextSanitizerTests.vb`, `Import/ImportPipelineTests.vb` (composer, flag finder, importer),
`Documents/DocumentsTests.vb` (checker, counter, model), `Documents/FileAndRecoveryTests.vb`,
`Documents/PasteAndScaffoldTests.vb`, `Documents/HebrewAndCommandLineTests.vb`,
`Documents/TextSearchTests.vb`, `Session/DocumentFormStateTests.vb`, plus tests added to
`Settings/WordWrapSettingTests.vb`. All pass. Test inputs are deliberately hostile (control
characters, lone surrogates, half-typed markup, seeded random garbage, runaway regexes, locked and
corrupt files), per the reliability guide.

### Documentation

`docs/omskep-phase2-final-checks.md` (the manual test checklist used for sign-off) and this file.
README and ATTRIBUTIONS are updated.

## Files changed

- `Settings/AppSettings.vb`: new saved settings `wordWrap`, `reviewMarks`, `directWrite` (all default on; older files load unchanged).
- `Session/AppSession.vb`: new `DefaultVoices()` and `WordWrapPreference`/`ReviewMarksPreference`/`DirectWritePreference` with setters. Setters swallow `IOException` (a preference is not worth an error).
- `MainForm.vb`, `MainForm.Designer.vb`: rewritten around the editor (File New/Open/Save/Save As/Export, Edit, View, Tools; status bar with note, check result and character count; 400 ms check timer; 5 s autosave; recovery prompt; close prompt; command-line open).
- Package references: `PdfPig` added to `Omskep.Core`; `Scintilla5.NET` 7.0.0 added to `Omskep.App`.
- `README.md`, `ATTRIBUTIONS.md`.

## Targets reached

All seven items in the phase 2 starter prompt:

1. **Editor wrapper** around Scintilla5.NET: line numbers, XML lexer, word wrap, squiggle and highlight indicators, DirectWrite on by default with a View setting to turn it off.
2. **Document model and open/save**: New, Open, Save, Save As, dirty star, close prompt. **Recovery was built in this phase** (decision with the developer): one atomic snapshot file per running instance, orphans offered at start, never deleted automatically.
3. **Import and sanitization** (Critical): everything in the requirements' "Text sanitization" section, tested on the real characters (U+F098/U+F099 are a decorative ornament; the `‘n` quote; NFC of U+FB4B proven by a test; only `&`, `<`, `>` escaped; scanned-PDF detection; wide runs flagged, not removed). Nothing is ever silently stripped.
4. **Extraction strategy decided with evidence**: `ContentOrderTextExtractor`. On 66 pages of two PDFs (45-page Afrikaans study, 21-page English study with tables) both methods give identical characters in identical order; `page.Text` gives one line per page, which would make line-number notes useless. The developer saw no reading-order differences between the two methods on either file.
5. **Live well-formedness checking**: 400 ms debounce, squiggle, status message, F8 (error) and Shift+F8 (probable cause). Never edits; never blocks saving.
6. **Export gating hook**: `DocumentFormState.ExportEnabled` (access allows AND document well-formed AND not an untouched blank document) with `ExportBlockedReason`.
7. **Billable-character upper bound** in the status bar, linear time, instant on 80,000 characters.

Added beyond the starter prompt, at the developer's request: Find and Replace with regular expressions (so
headers can be removed in one pass), Edit > Reverse Hebrew text, Paste vs Paste as plain text, review-mark and
DirectWrite toggles, open-from-command-line (double-click, Open with), plain-text file import with encoding checks.

## Deviations from plan

- **`DocumentFormState` is separate from `MainFormState`.** The starter prompt said to extend `MainFormState`. A new class avoids changing a signature that existing tests and `MainForm` use; the form uses both.
- **Recovery moved from phase 6 into phase 2**, as the starter prompt allowed. Phase 6 only needs to review it.
- **The sanitizer does not collapse interior space runs.** The phase 0 handoff said "collapse whitespace runs"; the requirements say wide runs are flagged, not removed, and the 61-space header gap is the signal that identifies a header. Trailing spaces and tabs are trimmed. Requirements won.
- **Pages are joined by one blank line** so each page's header is easy to see.
- **The "remove repeated lines" helper discussed in the plan was not built.** Find and Replace with a regular expression does the job and is more general (README has the recipe).
- **The status-bar count is an upper bound for the document as written.** Phase 3 adds prosody wrappers and a disclosure line and must show its own final composed count.
- **`ArticleQuote` is the straight apostrophe**, chosen by the developer; 346 straight versus 22 wrong-direction in the largest study made it the document's own majority form. Not yet validated by ear (see phase 3).

## Known issues or deferred items

- **Autosave runs every 5 seconds** (accepted by the developer): a kill can lose the last few seconds. The "snapshot on pause" improvement was declined.
- **Not tested**: Windows sign-out or restart with unsaved edits (test 3.1g skipped). The code path (`CloseReason.WindowsShutDown` writes a snapshot and skips the prompt) is unverified in practice.
- **Not reproducible**: a file held with an exclusive lock by another program (4.6). Covered by a unit test only.
- **Drag-and-drop of text into the editor bypasses the sanitizer** (it behaves like Paste as plain text). Not intercepted.
- **Hebrew order**: reversal is user-initiated, never automatic. In pointed text a vowel mark can land beside the wrong letter (marks are not spoken). Pure display direction (RTL markers) was considered and rejected: markers cost billable characters and cannot fix letter order.
- **Review marks** are recalculated on open, import, restore and paste, then follow the text as it is edited. They are not recomputed continuously.
- **Formal `.ssml` registration** (friendly name, icon, "Open with" entry without the manual step) is deferred to phase 6. "Open with, Choose another app, Always" already works once.
- **Second window**: double-clicking a file while Omskep is running opens a second instance. Recovery is per instance, so this is safe, but there is no single-instance forwarding.
- **PdfPig exception names**: password-protected PDFs are recognised by exception type *name* (`PdfDocumentEncryptedException`), so the Core file has no compile-time dependency on PdfPig's namespace. Worth one real password-protected PDF check if it was not exercised in the manual run.
- **Open recovery detail**: at start only one restored document can be active; older leftovers are kept and offered at the next start.
- **The text count and the check result refresh about 0.4 s after typing stops**, not on every keystroke.

## Verification outcome

Checklist from `workflow.md` and `docs/omskep-phase2-final-checks.md`, run by the developer on the real 45-page
Afrikaans study and the English study with tables:

1. Compiles with no warnings or errors; all unit tests pass.
2. App launches; locked until a key works (phase 1 behaviour unchanged).
3. Happy path: import, clean, save, reopen, content identical (diff checked). Passed.
4. Must-never checks: edits recoverable after End task (restore, cancel and discard all work); malformed markup reported and never fixed; scanned/image-only and password cases give clear messages; nothing silently stripped (diff against the extraction output); saving never corrupts the file. Passed.
5. Edge cases (more than two): Word text with curly quotes and a literal `&`; half-typed tags without flicker; line numbers and marks staying correct after edits and in the lower half of the document; Hebrew; narrow window; non-UTF-8 files; read-only save location. Passed after the fixes below.
6. Responsiveness on the largest document: typing, scrolling, undo and count smooth.
7. Window resize and word wrap behave; settings persist.
8. Placeholder audit: only the documented **Export placeholder** remains (`MainForm.mnuExport_Click` shows a message).

Defects found during manual testing and fixed in this phase: status-bar text overlapping when narrow; caret-line
colour too dark; no-key banner showing behind the recovery prompt; word wrap not remembered; review marks lost on
reopen; "pasted text" wording for a file import; Hebrew from PDFs reading in reverse (new command); and a real bug
where positions were converted to UTF-8 bytes although the control counts characters, which shifted whole-document
edits and review marks in text containing accented or Hebrew characters. The control's unit is now **measured at
startup** (`CalibratePositions`) instead of assumed, and `ApplyEdits` verifies its result and undoes itself if it
differs. A non-UTF-8 test file made in Notepad turned out to be UTF-8 already, so a real Windows-1252 file was used
for the encoding check.

Steps skipped: 3.1g (sign-out test) and 4.6 (exclusive lock), as noted above.

How results were recorded: the developer reported deviations and defects from the checklist; checklist items not mentioned as failing
were recorded as passed. That includes the scanned-PDF, password-protected-PDF and damaged-file messages, so those three are the first to
re-check if anything looks off in phase 3.

## Suggested coordinator update

### Phase 3 (export)

- **Document shape.** The editor text is LF-only, UTF-8 on disk. A document is `<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="..."><voice name="...">` + body + `</voice></speak>`, one line per tag. The body is sanitized text with `&`, `<`, `>` escaped; the user may add any SSML elements.
- **Gating.** Read `DocumentFormState.From(access, documentModel, count).ExportEnabled`. Before sending anything, re-run `WellFormednessChecker.Check` on the *final composed* text: the editor state is for display, the export must not trust it.
- **Counting.** Call `BillableCharacterCounter.CountUpperBound` on the final composed SSML (after prosody wrappers and the disclosure line) and show that number before export. The editor's number is a lower figure by design.
- **Writing files.** Use `AtomicFile`. `DocumentFile.Save` is for `.ssml` text only.
- **Settings.** `AppSession.DefaultVoices()` exists; settings accessors follow a try/Update pattern. New settings go in `AppSettings` with a safe default.
- **Listening checks owed from phase 2**: (a) does the straight `'n` read correctly in Afrikaans (it affects 346 places in the largest study); (b) does a Hebrew word, after Reverse Hebrew text, come out right with the Hebrew voice; (c) a mid-sentence line break (the extracted text is hard-wrapped at about 70 characters) must sound like a space.
- **Export button.** Replace the placeholder in `MainForm.mnuExport_Click`. The enabled state and tooltip are already driven by `DocumentFormState`.

### Phase 4 (voice authoring, mismatch warning, reference panel, rate presets)

- Use `SsmlEditor` for all edits: `InsertAtCaret`, `ReplaceSelectionText`, and `ApplyEdits(edits, expectedText)` for multi-place changes. `ApplyEdits` takes `TextEdit` offsets (character offsets in `DocumentText`, last edit first, non-overlapping), is one undo step, and returns False after undoing itself if the result differs. It is the safe way to change many places.
- Editor API positions are 1-based line and column in .NET characters. The control's own unit (bytes or characters) is measured once and hidden inside the class.
- `DocumentScaffold.BodyStartLine` and `ScaffoldDefaults.Choose` define where the first body text and the first voice come from.
- `ShowCheck` and `ShowFlags` are draw-only marks; add new indicator numbers from 11 up (0-7 belong to lexers; 8, 9, 10 are error, cause, review marks).

### Phase 5 (pronunciation preview and replacement list)

- `TextSearch.ReplaceAll` (literal or regex, whole word, match case, runaway protection) plus `SsmlEditor.ApplyEdits` already implement scoped, undoable, verified bulk replacement. Build the replacement list on them rather than writing a second engine. Zero-length matches are deliberately ignored.

### Phase 6 (polish)

- Review the recovery design once more (per-instance files, orphan rules, 5 s interval) and run the sign-out test.
- Register the `.ssml` type properly (name, icon) and decide about single-instance forwarding.
- Decide about intercepting drag-and-drop into the editor.
- Re-check the PdfPig and Scintilla5.NET licenses against the pinned versions and finish the notice files (see ATTRIBUTIONS).

### Suggested additions to `workflow.md` (VB.NET / WinForms)

- MSTest 4 analyzers are errors under warnings-as-errors. `Assert.AreEqual(n, collection.Length)` fails MSTEST0037; use `HasCount`/`IsEmpty`. `Assert.DoesNotContain(char, string)` is ambiguous; pass strings.
- VB: single-line lambdas cannot contain `Dim` or `Throw`; a statement cannot start with `New X(...)`; a parameter named `path` or `file` hides the `Path` and `File` classes; `Default` needs brackets (`Style.[Default]`).
- WinForms analyzer WFO1000: every settable public property on a control needs `DesignerSerializationVisibility(Hidden)` (or a default value).
- Scintilla5.NET: some members are obsolete (`CaretLineVisible`), the caret-line alpha is not applied, and positions may be characters or bytes: measure, do not assume.
- When the developer's tools cannot be run in the chat environment (WinForms, Scintilla), write the Core logic so it can be compiled and tested there, and keep the form thin.
- Long phases exhaust the chat session limit. Present files as soon as they are written, and keep each step small.

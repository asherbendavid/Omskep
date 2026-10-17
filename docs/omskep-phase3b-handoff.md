# Omskep phase 3b handoff -- the export dialog, ID3 tagging, and document metadata

## Phase goal

Make the synthesis engine built in phase 3a actually reachable from the application.
Starting point: `MainForm.mnuExport_Click` was a placeholder, `ExportEngine` had no
caller, there was no file naming, no ID3 tagging, and no document metadata of any
kind. End state: **File > Export** opens a confirmation dialog, writes a Save
As-chosen MP3 with full ID3v2 tags, and offers **Try Again** after a failure without
re-synthesizing audio that already succeeded. Along the way, this phase also closed
the test gap flagged at the end of phase 3a and completed two of the three engine-
hardening items that handoff left open.

This phase grew substantially past its starter prompt during the architect-first
discussion that preceded any code: the starter prompt assumed ID3 metadata (Artist,
Album, Track, Year, Copyright) could simply be typed into an export dialog each time,
but working through it surfaced that this information has nowhere to live between
exports of the same document, and nowhere at all for a PDF's own metadata. The
result -- an optional, hand-editable, independently-specified metadata block in the
document's own SSML prolog -- is the single largest addition beyond the original
plan, and is documented as its own open file format, not an Omskep implementation
detail, in `docs/omskep-meta-spec.md`.

## Files added

- `docs/omskep-meta-spec.md` -- the `omskep-meta` file format: an optional processing
  instruction in the SSML prolog, one `Key: Value` field per line (first colon
  splits key/value, keys case-insensitive, first occurrence of a duplicate key wins,
  blank or absent both mean null, a malformed or unrecognised line is silently
  ignored). Defines Title, Artist, Album, Track, Year, Copyright, Comment, Genre,
  plus several fields reserved for a later phase (AlbumArtist, Composer, Conductor,
  Publisher, Disc, Grouping, Subtitle, Language). Written to be independently
  implementable by another tool, with an informative (non-normative) section at the
  end describing Omskep's own use of it.
- `src/Omskep.Core/Documents/OmskepMetaFields.vb` -- the parser, matching the spec
  exactly. Generic over field name (recognises every field the spec lists, not just
  the ones Omskep currently reads), so a future phase reading a reserved field needs
  no parser change.
- `src/Omskep.Core/Export/ExportPreparation.vb` -- re-runs the export gate at the
  moment of export (never trusts the menu's last-enabled state), composes, chunks,
  resolves ID3 fields, and picks the default filename, in one call. `ExportPreparationResult`.
- `src/Omskep.Core/Export/ExportId3FieldsBuilder.vb` -- resolves the omskep-meta
  block plus the computed Comment (voices used, speaking rate, synthetic-speech
  disclosure, source filename) and Genre (`Audiobook`); a hand-typed Comment or
  Genre always wins outright over the computed value, no in-between choice.
- `src/Omskep.Core/Export/VoiceUsageScanner.vb` -- distinct `<voice name>` values
  used across a document's chunks, first-appearance order (sorting for display
  happens at the point of formatting, in `ExportId3FieldsBuilder`).
- `src/Omskep.Core/Export/Id3Stamper.vb` -- thin wrapper over TagLibSharp2's
  `Mp3File`, stamping all eight fields onto the already-written, untagged MP3.
  `Id3Fields`, `Id3StampResult`.
- `src/Omskep.Core/Export/ExportFileNamer.vb` -- `<document name> Alpha.mp3`,
  bumping to `(2)`, `(3)` on collision; purely a Save As default, never enforced.
- `src/Omskep.App/ExportForm.vb` (+ `.Designer.vb`) -- the dialog itself: read-only
  confirmation screen (billable count, all eight resolved fields, a Copy button on
  Comment) -> Save As -> progress -> a result screen offering Try Again on failure.
- Tests: `OmskepMetaFieldsTests.vb`, `ExportId3FieldsBuilderTests.vb`,
  `VoiceUsageScannerTests.vb`, `ExportFileNamerTests.vb`, and a large expansion of
  `ExportEngineTests.vb` (see Targets reached).

## Files changed

- `src/Omskep.Core/Export/ExportEngine.vb` -- optional injectable retry-delay
  constructor parameter (testability only, defaults to the real 3 seconds); new
  `ExportStatus.FailedWriteOnly` and `ExportResult.FailedWriteOnly(...)`; content-
  checked resume (`ChunkExportOutcome.ContentHash`, a SHA-256 of the chunk's SSML,
  checked per chunk during resume rather than trusting position/status alone).
- `src/Omskep.Core/Session/AppSession.vb` -- constructor now also takes the
  `ISpeechClient` explicitly (previously buried, inaccessible, inside `VoiceService`);
  new `CreateExportEngine()` and `SpeakingRatePercent()`. This is a breaking
  constructor signature change; every caller in this repo was updated as part of
  this phase (see below), but it's worth knowing about if any other caller exists
  outside what this phase touched.
- `src/Omskep.Core/Documents/SsmlComposer.vb` -- strips every top-level node except
  the root element (the omskep-meta block, any other prolog/epilog comment or
  processing instruction, a stray XML declaration) after parsing, before composing.
  Narrower than it sounds: `FindNestedVoice`/`CollapseWhitespace` already only ever
  touched `doc.Descendants(...)` (element-only), so the only actual leak was
  `doc.ToString()` serializing top-level nodes verbatim; this is the one line fixed.
- `src/Omskep.Core/Documents/DocumentScaffold.vb` -- `BodyStartLine` is now `11`
  (was `3`: the 8-line omskep-meta block shifts speak/voice/body down); `IsBlank`
  now also recognises the omskep-meta block as free, via the new
  `BillableCharacterCounter.EndOfMetaBlock`.
- `src/Omskep.Core/Documents/BillableCharacterCounter.vb` -- new
  `EndOfMetaBlock`, checked before the existing generic comment/CDATA/PI handling
  (which deliberately bills every *other* PI as plain text, unchanged). The two
  must agree on exactly what qualifies as "the omskep-meta PI" (same literal target
  name, same "whitespace immediately after" requirement) as `OmskepMetaFields.Parse`'s
  own regex -- a real coupling between the two files, not compiler-enforced.
- `src/Omskep.Core/Import/DocumentComposer.vb` -- `Compose` gains six optional
  parameters (title/artist/album/track/year/copyright) seeding the new block;
  every existing caller compiles unchanged (all six default to unset).
- `src/Omskep.Core/Import/ImportTypes.vb` -- `IPdfPageReader` gains
  `ReadMetadata(pdfPath) As PdfDocumentMetadata`; new `PdfDocumentMetadata` DTO
  (raw PDF Title/Author/CreationDate/ModificationDate, Nothing for anything unset).
- `src/Omskep.Core/Import/PdfPigPageReader.vb` -- implements `ReadMetadata` via
  `PdfDocument.Information`. **Not yet verified against the real PdfPig API** --
  specifically whether `ModifiedDate` is the exact real property name (vs. `ModDate`
  or similar). Flagged, not fixed, since I (the assistant) could not check this
  against the actual package.
- `src/Omskep.Core/Import/DocumentImporter.vb` -- `ImportPdf` now reads PDF
  metadata and the study leader's `D_name (language).pdf` filename convention to
  seed Title (PDF metadata, else the filename's `name (language)` portion, else the
  whole filename), Artist (PDF Author), Track (the filename's leading number), and
  Year (the PDF's creation date, falling back to modification date). Album and
  Copyright are never seeded on import, by design.
- `src/Omskep.App/PasteHandler.vb`, `tests/.../HebrewAndCommandLineTests.vb`,
  `tests/.../ImportPipelineTests.vb` -- trivial `ReadMetadata` additions to every
  other `IPdfPageReader` implementer (one production no-op, two test fakes) so the
  interface addition above compiles everywhere.
- `src/Omskep.App/MainForm.vb` -- `mnuExport_Click` replaces the phase-1 placeholder.
- `README.md`, `ATTRIBUTIONS.md` -- phase 3 marked complete; new "Document metadata"
  and "Exporting audio" sections; TagLibSharp2 moved to in-use.

## Targets reached

All of phase 3a's own flagged test gap, plus all of this phase's starter-prompt
items, plus the full omskep-meta addition:

1. **3a's test gap, closed.** `ExportEngineTests.vb`: happy path (byte-exact join),
   401 (no retry, sticky session-wide lockout), 429/offline/5xx/timeout (retry then
   recover), retry exhaustion, implausibly-short/truncated audio, cancellation
   (both between chunks -- clean `Cancelled` -- and mid-chunk, which **throws
   `OperationCanceledException` instead**, documented as a real, deliberate-to-leave
   gap, not fixed), resume (plain, and with an edited chunk -- proving the new
   content-hash check), a vanishing key mid-export, no key ever leaking into any
   message, and the write-only-failure case with its own resume. Confirmed
   already-present: `Chunker`'s `TotalBillableCharacters` tests and nested-`<voice>`
   rejection test, both from 3a, both still passing.
2. **Engine hardening, two of three items done, one explicitly declined.**
   `ExportStatus.FailedWriteOnly` and content-checked resume: done, tested. The
   stronger truncation guard (option B/C from the 3a handoff): **declined by
   developer decision** -- character-count-based duration estimation was judged
   fundamentally unreliable ("888" vs. "eight" as the illustrating case), and the
   existing absolute-floor check plus the human listen-through was kept as
   sufficient for the MVP, with "keep chunks to only re-fetch the faulty ones"
   logged as a possible future improvement if this ever proves to be a real problem
   in practice.
3. **ID3 stamping.** All eight fields, `TagLibSharp2`. Decided and built in this
   order: Comment/Genre get a presence-wins rule (no radio choice, no in-dialog
   editing of any kind -- see Deviations); Title/Artist/Album/Track/Year/Copyright
   come straight from the document's own omskep-meta block, full stop.
4. **The naming function.** Simplified from the original requirements doc's
   Alpha/RC-counter scheme down to Alpha-only (see Deviations).
5. **The export dialog and wiring.** Built, including the Save As step, progress
   reporting, Cancel, and a Try Again button not originally planned until a manual
   test session surfaced that resume was otherwise unreachable from the UI (see
   Known issues below -- this was caught and fixed, not left outstanding).
6. **User-facing failure wording.** Done per status: key/region rejection, busy/
   transient, offline, a chunk failing after retries (naming which chunks finished,
   noting possible double-billing), and the write-only-failure case ("audio fine,
   saving failed").
7. **Checked by listening**, by the developer, via the manual test checklist
   (`docs/phase3b-manual-test-checklist.md`) covering every path above except two
   (see Known issues).

## Deviations from plan

- **Document metadata (omskep-meta) did not exist in the starter prompt at all.**
  It emerged from discussing where Artist/Album/Track/Year/Copyright could
  possibly come from across multiple exports of the same document, and from PDF
  import. Decided, in order: a processing instruction (not a comment -- comments
  can never contain `--`, a real risk in hand-typed values) in the prolog (not
  epilog, not inside `<speak>` -- lowest risk to `SsmlComposer`/`Chunker`'s tested
  internals); one `Key: Value` per line split on the first colon (not `key=value`,
  not quoted); case-insensitive keys; first-occurrence-wins on a duplicate key
  (the developer's own reasoning: whoever scans the file will fix the first one
  they see); a full, published field vocabulary with most fields reserved rather
  than wired up, specifically so the format is useful to a third party without
  waiting on Omskep's own roadmap.
- **Naming simplified from Alpha/RC-counter to Alpha-only.** The developer's actual
  workflow doesn't need the app to know about Release Candidates at all -- Alpha is
  always what's exported for the developer's own internal check; RC naming is a
  rename/copy the developer does by hand once satisfied. Every export still opens a
  Save As dialog, so nothing is lost -- the app's naming opinion is just smaller.
- **"Remembered last-used Album" (an `AppSettings` property, per the original
  starter prompt) was proposed, then explicitly withdrawn.** Having a value seeded
  from Settings as well as the document's own field would have created two sources
  of truth for the same information. Final rule: the document is the only source of
  truth for every field; Settings holds nothing export-related.
- **The export dialog's fields are entirely read-only; nothing is editable in it,
  including Comment and Genre.** An earlier plan had Comment/Genre as a radio choice
  between typed and computed; this was dropped for the same reason as the Album
  setting above -- any choice made in the dialog that isn't also reflected back into
  the file is a second, temporary source of truth. Final rule: a typed value in the
  document always wins outright; the dialog only *displays* which one is in effect,
  plus a Copy button on Comment specifically, so a developer who wants to lock in a
  particular computed value can paste it into the file by hand.
- **PDF metadata extraction (`PdfDocument.Information`, plus the filename-number
  parsing convention) was originally assumed out of scope for ID3 work and only
  added after discovering `PdfPigPageReader` extracts page text only.** This is a
  real, if small, assumption about the study leader's own filename convention
  continuing going forward (`D_name (language).pdf`); the developer explicitly
  accepted this risk with a graceful fallback (whole filename, no Track) for any
  file that doesn't match.

## Known issues or deferred items

- **Cancellation while a chunk is in flight throws `OperationCanceledException`
  out of `ExportEngine.ExportAsync`, rather than returning a clean `Cancelled`
  result.** `ExportForm` catches this specifically and shows a plain "cancelled,
  nothing saved" message, with no resume offered (there is no `ExportResult` to
  resume from in this path). This is a real, acknowledged design asymmetry in
  `ExportEngine` itself (clean cancellation is only ever observed *between*
  chunks), not something this phase fixed -- flagged for phase 4+ to decide whether
  it's worth unifying.
- **`PdfPigPageReader.ReadMetadata`'s exact PdfPig property name for the
  modification date was never verified against the real package** (I, the
  assistant, only had web search available, not the installed library). Check
  `info.ModifiedDate` compiles as written before trusting Year-from-modification-
  date on a PDF with no creation date.
- **Two manual test-checklist items (section 8's write-failure and resume-without-
  resynthesis provocations) were not run** -- no file-locking tool was available in
  the developer's environment. Both paths have real, passing automated coverage
  (`ExportEngineTests.Export_AllChunksSucceed_ButWriteFails_...` and its own resume
  assertion), so this is a gap in manual provocation only, not in test coverage.
- **Three real bugs were found and fixed during manual testing, after the automated
  suite was already fully green**, worth remembering as a caution for future UI
  work specifically: the progress bar not reaching 100% on success (no final
  "done" report was ever sent); the Cancel button closing the dialog silently
  instead of reporting cancellation (a `DialogResult` set on the button itself in
  the Designer overriding the intended async flow -- a classic WinForms trap, not a
  logic bug); and resume being completely unreachable from the UI despite being
  thoroughly engine-tested, because no "Try Again" affordance existed until a
  manual test session surfaced the gap. All three are fixed and re-verified. The
  general lesson -- unit-testing `ExportEngine` in isolation did not, and could not,
  catch any of these -- is worth keeping in mind for phase 4's own UI work.
- **`spike/` is still present, gitignored, not committed** -- per the developer's
  own standing plan from phase 3a, to be deleted at the end of phase 3 as a whole.
  Worth doing now, since phase 3 (3a + 3b) is now fully complete.
- **Nothing from this phase has been committed to git yet.** The suggested commit
  message (already drafted, in this conversation) groups the change logically; the
  developer may choose to split it into several smaller commits using those groups
  as a guide, or commit as one. Either way, this should happen before phase 4 work
  begins, and before `spike/` is deleted, so that deletion is its own clean commit.

## Verification outcome

1. **Compiles with 0 warnings/errors; all tests pass** -- confirmed by the
   developer throughout, incrementally, at every step of this phase; 511+ tests
   passing at the end (up from 480 at the start of this phase).
2. **Manual test checklist** (`docs/phase3b-manual-test-checklist.md`) -- run in
   full by the developer; every item passed except the two file-locking
   provocations noted above (automated coverage substitutes). Three real bugs
   found and fixed during this pass, all three re-verified afterward (see Known
   issues).
3. **Listening check** -- done by the developer on a real multi-chunk export
   through the actual UI path, including non-ASCII (Hebrew, Afrikaans diacritics)
   titles/comments, verified in both foobar2000 and Windows Explorer's tag view.
4. **Must-never checks**: no export without the gate re-checked at export time
   (`ExportPreparation.Prepare`, called fresh every time, never trusting the
   menu's last-enabled state); no key in any message (tested); no file left behind
   after a failed or cancelled export (tested, and manually confirmed); the
   omskep-meta block never reaches Azure or the billable count (tested at the
   `SsmlComposer`/`BillableCharacterCounter` level, and implicitly confirmed by the
   listening check containing no stray spoken metadata).
5. **Regression of phases 1-3a**: confirmed by the developer, including opening a
   genuine pre-phase-3b `.ssml` file with no omskep-meta block at all, confirming
   the parser's "absent = null" behavior and the whole pipeline hold up for a real
   legacy file, not only test fixtures built with the new scaffold shape.
6. **Placeholder audit**: `mnuExport_Click` is no longer a placeholder; nothing new
   added.

## Suggested coordinator update

- **For phase 4 (voice authoring):** the omskep-meta spec's reserved-field list
  (`AlbumArtist`, `Composer`, `Conductor`, `Publisher`, `Disc`, `Grouping`,
  `Subtitle`, `Language`) is already parseable today, even though nothing reads
  them yet -- if phase 4 wants to surface any of these, no parser change is
  needed, only reading `OmskepMetaFields.Value("...")` and deciding what to do
  with it. None of these names should be reused for anything else. `Language` in
  particular may be worth a second look against whatever phase 4 designs for
  per-sentence language tagging -- this field was reserved for a document-level,
  free-text language statement, which may or may not be what phase 4 actually
  needs.
- **The export gate (`DocumentFormState.ExportEnabled`) and `AppSession.Access`
  interact in a way worth knowing before building anything else that calls
  export:** a 401 during an export attempt sets a *sticky* rejection on the
  session's shared `ConnectionMonitor`, which blocks `CanExport` for every
  subsequent attempt until the developer revisits Settings -- this is
  `ExportEngine`'s own existing (3a) behavior, re-confirmed relevant here because
  the export dialog's Try Again button deliberately does **not** attempt to work
  around it; a 401 gives no Try Again option at all, only Close.
- **`ExportForm`'s Try Again mechanism is generic over `ExportStatus`**, not
  hardcoded to any one failure kind -- a future new failure status would only need
  a new `Select Case` branch deciding `canTryAgain`/the message text, not new
  plumbing. Worth keeping in mind if phase 4+ ever adds a new way for export to
  fail.
- **`AppSession`'s constructor now takes an explicit `ISpeechClient`** -- any new
  code constructing an `AppSession` directly (production or test) needs this fifth
  argument; `AppSessionTests.Rig` and `ExportEngineTests.Rig` are both already
  updated as the reference pattern.

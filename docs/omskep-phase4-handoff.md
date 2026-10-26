# Omskep phase 4 handoff — voice authoring, mismatch warning, reference panel, rate presets

## Phase goal

Make the editor good at what the app exists for: documents that mix voices
and languages. Split or rename the voice for a selection without
hand-editing tags; flag a hand-typed nested `<voice>` live; warn (or, for a
missing voice, block outright) before sending the wrong script to the wrong
voice; set a default voice and speaking rate per locale; and give the editor
a built-in SSML syntax reference. Phase 5 (pronunciation preview and the
replacement list) and phase 6 (polish) are still to come.

## Files added

**Core (`Omskep.Core.Documents`), each with its own MSTest file:**

- `VoiceSplitter.vb` / `VoiceSplitterTests.vb` — splits the enclosing
  `<voice>` around a selection, or renames it (no selection, or a selection
  covering a voice's whole trimmed content). Pure, Critical tier,
  catalog-agnostic by design. Every edit is an insertion or a single
  attribute-value replace against original offsets, applied through
  `SsmlEditor.ApplyEdits` as one undo step. Hand-rolled tag scanner, not
  `XDocument`/`XmlReader` — it only has to handle text `WellFormednessChecker`
  has already approved, and needs precise original-offset insertions, which a
  round-trip through `XDocument.ToString()` can't give cheaply.
- `NestedVoiceFinder.vb` / `NestedVoiceFinderTests.vb` — finds every `<voice>`
  that opens while another is already open, for live marking. Unlike
  `VoiceSplitter`, this runs on every keystroke against text that is often
  mid-edit and not well-formed, so it's deliberately lenient: an unterminated
  tag just stops the scan rather than throwing.
- `VoicePickerListBuilder.vb` / `VoicePickerListBuilderTests.vb` — the
  ordered, locale-grouped voice list (document's default locale first,
  locales already used next, the rest last, each alphabetical) shared by
  both picker entry points.
- `VoiceAttributeLocator.vb` / `VoiceAttributeLocatorTests.vb` — "is this
  document position inside a `<voice name="...">` value," for the
  context-menu entry point. Lenient, like `NestedVoiceFinder`.
- `VoiceBlockAnalyzer.vb` / `VoiceBlockAnalyzerTests.vb` — walks composed
  SSML's `<voice>` elements once, resolving each block's locale (catalog
  first, falling back to the "ll-CC-..." short-name shape) and catalog
  membership, feeding both of the next two:
  - the export-time voice-name gate (hard block, in `ExportPreparation.vb`)
  - `ScriptMismatchDetector.vb` / `ScriptMismatchDetectorTests.vb` — warns
    when a block contains *any* letter in a script other than its locale's
    expected one (a data-driven locale-to-script table; a new locale is a
    one-line addition).

**App (`Omskep.App`), hand-written Designer-paired forms (see "Deviations"
for how to add them to the project correctly):**

- `VoicePickerForm.vb` / `VoicePickerForm.Designer.vb` — the searchable,
  locale-grouped picker dialog over `VoicePickerListBuilder`'s output.
- `VoiceSettingsForm.vb` / `VoiceSettingsForm.Designer.vb` — per-locale
  default voice and speaking rate (-90% to 100%), under
  **Tools > Voice settings...**.

**Docs / test assets:**

- `docs/phase4-manual-test-checklist.md` — the companion manual checklist,
  run in full against every block plus a regression pass of phases 1-3b; all
  items confirmed.
- `break-timing-test.ssml` — not part of the product; a small standalone
  document built to measure real `<break strength="...">` durations in
  Audacity for the reference panel's Pause entry. Worth keeping somewhere
  findable (not necessarily committed) in case Azure's timing ever changes
  and the measurement needs redoing.

## Files changed

- `MainForm.vb` / `MainForm_Designer.vb` — "Use another voice..." (Edit menu
  and context menu), the context-menu hook that opens the picker instead of
  the normal menu when the caret is in a voice-name attribute, "Voice
  settings..." (Tools menu), "SSML reference" (View menu) and the panel
  itself (`BuildReferencePanel`), and the nested-voice check wired into the
  existing debounced `RunCheckNow()` cycle.
- `SsmlEditor.vb` — indicator 11 (`NestedVoiceIndicator`, purple squiggle;
  next free is 12), `ShowNestedVoiceMarks`, `CaretIndex`.
- `AppSession.vb` — `SetDefaultVoice` / `SetSpeakingRatePercent`, mirroring
  `SetWordWrapPreference`'s existing shape exactly (try/catch `IOException`
  around `SettingsStore.Update`, no `RaiseStateChanged` — a quiet preference,
  like word wrap and review marks).
- `ExportPreparation.vb` — `ExportPreparationResult` gained `Warnings`; the
  voice-name gate and script-mismatch detection run on `composition.Ssml`
  (post-compose, pre-chunk) via `VoiceBlockAnalyzer`.
- `ExportForm.vb` — the "export anyway" confirmation (see "Deviations" for
  why it's a dialog, not an inline panel).
- `ExportId3FieldsBuilder.vb` — fixed by the developer directly, not in this
  phase's own blocks; see "Known issues," first entry.
- `README.md`, `ATTRIBUTIONS.md` — phase 4 status, the two new feature
  sections ("Choosing and switching voices," "SSML reference panel"), project
  structure, and the attribution note correcting an earlier phase's
  speculative "a later phase will link to it" line now that the panel is
  real and doesn't link out.

## Targets reached

All six items from the phase 4 starter prompt:

1. **Voice authoring** — `VoiceSplitter` plus both picker entry points
   (`VoicePickerForm` via the Edit/context menu, and directly on an existing
   `<voice name="...">` via right-click or the context-menu key).
2. **Live structural marking** — `NestedVoiceFinder` + indicator 11.
3. **Script/voice mismatch warning** — `ScriptMismatchDetector`, merged with
   item 6 below on the developer's own call, since both need the same
   per-`<voice>`-block walk.
4. **Per-language rate presets in the UI** — `VoiceSettingsForm`.
5. **SSML reference panel** — five verified, click-to-copy examples.
6. **Voice-name export gate** (originally phase 5's item 1, pulled forward
   and merged with item 3) — hard block, `VoiceBlockAnalyzer` +
   `ExportPreparation`.

**Explicitly deferred, as agreed at the start of this phase:** the
`OperationCanceledException`-out-of-`ExportAsync` inconsistency (item 6 of
the original starter prompt's numbered list) goes to phase 6 with the rest of
the polish items.

## Deviations from plan

- **Blocks 6 and 7 merged** into one deliverable (developer-approved,
  reflected in `ScriptMismatchDetector`/`VoiceBlockAnalyzer`/
  `ExportPreparation` sharing one pass rather than two).
- **5b abandoned Scintilla's native `AutoCShow` dropdown** after two real
  bugs in manual testing: `AutoCOrder`'s default (`Presorted`) silently
  breaks matching against a relevance-grouped (not globally alphabetical)
  list, and there's no built-in way to clear text after the caret on a
  mid-value edit. Rather than keep fighting Scintilla-specific behavior,
  switched to opening `VoicePickerForm` directly from the same context-menu
  hook — reusing already-shipped, already-tested primitives
  (`SelectCharacterRange` + `ReplaceSelectionText`) instead. Net result is
  simpler than the native attempt would have been, and gained something the
  native version never had: the picker now opens pre-selected on whatever
  voice is already in the attribute.
- **`ScriptMismatchDetector`'s first design ("dominant script of the whole
  block") was a literal but wrong reading of the starter prompt's wording.**
  A single foreign-script word mixed into an otherwise-correct block — the
  realistic mistake, and per the developer's phase 0 reference, exactly what
  Azure silently drops rather than mispronounces — never flips a block's
  *overall* dominant script, so it went undetected entirely. Caught only by
  manual testing with real mixed-language content; the original unit tests
  encoded the same wrong assumption the code did, so they didn't catch it
  either. Redesigned to presence-based detection (any unexpected-script
  letter, not a majority) before being considered done.
- **"Export anyway" is a Yes/No confirmation dialog, not an inline panel** on
  `ExportForm`'s confirm screen. That screen is absolute-positioned with a
  fixed 800x320 size and ~26px of genuinely free space — carving out room
  for a proper inline panel would have meant real Designer-coordinate
  surgery, which felt like the wrong place to start guessing blind. The
  dialog satisfies "an explicit export anyway" literally and needed zero
  Designer changes.
- **Reference panel's break-strength durations are the project's own
  measurement**, not anything Microsoft publishes (Azure doesn't document
  exact per-keyword timing): x-weak ~180ms, weak ~410ms, medium ~700ms,
  strong ~915ms, x-strong ~1175ms, measured in Audacity and confirmed
  consistent across locale and voice (unadjusted for a small, consistent
  50-80ms measurement-artifact gap on the exact-time sanity checks — see
  `break-timing-test.ssml`).
- **Reference panel visibility is session-only, not persisted** — developer's
  call; unlike word wrap, review marks and DirectWrite, it always starts
  hidden.
- **Row sizing in both new dynamically-built dialogs needed a second pass.**
  `VoiceSettingsForm`'s rows first overlapped entirely (a plain `Panel`, not
  a stacking container, with no explicit `Location` on each dynamically
  added row — changed to `FlowLayoutPanel`) and then had their rightmost
  controls clipped (width computed from the container's `ClientSize` before
  accounting for the vertical scrollbar, which a 140+-locale catalog makes
  permanent — changed to a fixed constant, appropriate since the dialog
  itself is a fixed, non-resizable size). The reference panel repeated a
  related mistake independently (row height guessed from explicit-newline
  count, which doesn't account for word-wrapping) before being fixed with
  `TextRenderer.MeasureText(..., TextFormatFlags.TextBoxControl)` against
  the box's real width. **Worth remembering as a pattern for any future
  dynamically-built WinForms content in this app: measure the real rendered
  size, don't guess it from a proxy like explicit line breaks or a
  not-yet-finalized container width.**

## Known issues or deferred items

- **A latent bug in `ExportId3FieldsBuilder`, predating this phase, was
  exposed by `VoiceSettingsForm`.** The ID3 comment used to enumerate every
  locale in `SpeakingRatePercent`, which previously meant "a handful, if
  any." Once Voice Settings legitimately saves a rate for every catalog
  locale (140+), the comment became longer than the document's own SSML for
  a one-sentence test file. Fixed by the developer directly, outside this
  phase's own file set: the rate list is dropped from the ID3 comment
  entirely, since it was only ever the *default* rate and could already be
  stale against a manually-tagged document regardless. **Flagging explicitly
  for the coordinator since the fault doesn't belong to phase 4, even though
  phase 4 is what surfaced it.**
- **`VoiceSplitter`'s own `<`/`&`/`"` character guard on `newVoiceName` is now
  unreachable from the current UI.** Block 5a replaced the free-text
  `InputBox` with `VoicePickerForm`, so the only remaining caller
  (`ChangeVoice`) always passes a clean catalog short name. The guard is kept
  deliberately — `VoiceSplitter.Split` is a public Core function with its own
  contract, independent of what currently calls it — but worth knowing so a
  later reader doesn't wonder why that branch never fires in real usage.
  (Confirmed during manual testing: typing a literal `&` directly into the
  document's XML is correctly caught by the *well-formedness checker*
  instead, a different code path entirely; a properly escaped `&amp;` is
  valid XML and is correctly accepted, not a bug.)
- **The "ll-CC-..." short-name-to-locale guess is now duplicated in two
  places** (`VoicePickerListBuilder`, `VoiceBlockAnalyzer`), down from a
  third (the abandoned native-autocomplete attempt's copy, deleted along
  with the rest of that code). Still judged not worth extracting pre-emptively
  — a third live copy is the trigger point, same reasoning applied each time
  this came up this phase.
- **Natural (non-`<break>`) sentence-boundary pause duration was found to be
  locale/voice-dependent** (0.123s-0.600s across the voices tested) during
  the break-timing measurement session. Not part of the SSML reference panel
  (it's not something markup controls), but worth remembering for phase 5's
  pronunciation/replacement-list work, where it could matter.
- **Three real code bugs surfaced this phase**, each a different flavor, all
  caught before reaching a real document: `Assert.Contains`/`DoesNotContain`
  argument order (the analyzer's suggested replacement reverses the order
  from the original `.Contains()` call, non-obviously); VB's `Not` binding
  *looser* than binary minus, unlike C#'s `~` (broke a `BinarySearch`
  insertion-point calculation); and `SsmlEditor.ApplyEdits` requiring edits
  "last first," not the ascending order that seemed natural to write. Two
  were caught by the unit suite, two by manual testing (one spans both,
  since the wrong edit order only breaks on the two-edit case the unit tests
  happened to also need manual confirmation for). **Worth citing as evidence
  for the coordinator** that this phase's heavier-than-usual manual-testing
  discipline (including the developer's own standing request, mid-phase, to
  keep manual checks even on "trivial" code paths) earned its keep
  repeatedly, not just once.

## Verification outcome

1. **Compiles with 0 warnings/errors; all tests pass** — confirmed, 617/617.
2. **Happy path** (open a study, switch voices via both entry points, export,
   listen) — confirmed in manual testing throughout the phase and in the
   final checklist pass.
3. **Must-never checks**: nested `<voice>` never produced by the splitter,
   and marked/gated if hand-typed; a script mismatch (including a single
   foreign word, not just a whole-block mismatch) produces the warning with
   "export anyway"; a missing voice hard-blocks with no override; no edit
   ever leaves the document malformed; no automatic rate or voice change
   (rate is composition-time only, never written to the saved document) —
   all confirmed.
4. **Edge cases**: a selection across an element boundary is refused; a
   voice removed from the catalog produces the hard block at export; a
   document with no voice element is refused by the splitter; Hebrew with
   vowel points classifies correctly (vowel points aren't letters, so
   they're correctly skipped, not miscounted) — all confirmed.
5. **Manual UI checklist run in full, plus regression of phases 1-3b** —
   `docs/phase4-manual-test-checklist.md`, every item ticked.
6. **Placeholder audit**: none. The one placeholder introduced mid-phase (the
   plain `InputBox` standing in for a real picker in Block 2) was superseded
   by `VoicePickerForm` in Block 5a, before the phase shipped.

## Suggested coordinator update

- **For phase 5** (pronunciation preview and the replacement list, on top of
  `TextSearch.ReplaceAll` and `SsmlEditor.ApplyEdits`): the next free
  indicator number is 12 (0-7 lexers, 8 error, 9 cause, 10 flag, 11 nested-
  voice). `VoiceAttributeLocator`/`VoiceBlockAnalyzer`'s pattern (a lenient,
  hand-rolled scanner answering one narrow "where/what is this" question,
  feeding a UI entry point) may be a reasonable model if phase 5 needs
  similar caret-context detection for the replacement list. The natural
  sentence-pause-duration finding above (locale/voice-dependent) may be
  relevant there too. The two surviving duplicated locale-guess-heuristic
  copies are a watch item, not yet worth extracting.
- **The `ExportId3FieldsBuilder` fix** (ID3 comment no longer lists a rate
  per locale) belongs to no phase in particular — flagging it here mainly so
  it isn't rediscovered cold later and mistaken for a phase 4 regression.
- **Process note, already acted on but worth writing into `workflow.md`
  itself**: this phase's three real bugs, plus the `ScriptMismatchDetector`
  design miss, were caught almost entirely by manual testing exercising
  *real* content (the developer's own mixed-language test case, the real
  Audacity measurements) rather than by reasoning about the code in the
  abstract — including on code that looked, and tested, as obviously correct
  beforehand. Worth stating explicitly as a standing rule rather than
  leaving it as something phase 4 happened to do well: manual checks don't
  become optional once a code path looks trivial or once unit tests are
  green.
- Cancellation unification (`OperationCanceledException` out of
  `ExportAsync`) remains deferred to phase 6, as agreed at the start of this
  phase.

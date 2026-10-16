# Omskep phase 3a handoff — the synthesis engine

## Phase goal

Build and prove the synthesis engine in `Omskep.Core`: composing a document
into the SSML actually sent to Azure, splitting it into chunks under Azure's
duration limit, calling the Speech REST endpoint, and running the chunks
through to one joined, crash-safely-written MP3 with retry — with unit tests
and real-Azure listening verification. No export UI, no ID3 tagging, no file
naming: that is phase 3b.

## Files added

- `src/Omskep.Core/Speech/SynthesisResult.vb` — result of one `SynthesizeAsync`
  call (outcome, status, detail, audio bytes, Retry-After).
- `src/Omskep.Core/Documents/SsmlComposer.vb` — builds send-ready SSML from a
  document: well-formedness gate, no-nested-`<voice>` structural rule,
  whitespace collapse, per-locale `<prosody rate>` wrapper, final
  well-formedness re-check. `CompositionResult`.
- `src/Omskep.Core/Documents/Chunker.vb` — splits composed SSML into chunks
  under the duration cap. Whole-`<voice>`-element ("free") splits first;
  sentence-end ("reconstructed") splits only inside a single oversized voice
  element; reports rather than forces when neither exists.
  `Chunker.TotalBillableCharacters` sums the billable count across chunks.
  `DocumentChunk`, `ChunkingResult`.
- `src/Omskep.Core/Export/ExportEngine.vb` — sequential per-chunk synthesis
  with retry, a plausibility check on each response, byte-join, crash-safe
  write via `AtomicFile`. Same-session resume. `ChunkExportOutcome`,
  `ExportResult`, `ExportProgress`.
- `tests/Omskep.Core.Tests/Documents/SsmlComposerTests.vb`,
  `tests/Omskep.Core.Tests/Documents/ChunkerTests.vb` — MSTest, see
  Verification outcome below for what they cover.
- `spike/Spike.Synthesis/Program.vb`, `spike/Spike.Pipeline/Program.vb` —
  throwaway harnesses (one direct `.ssml` → MP3, one full
  compose → chunk → synthesize-per-chunk → join). Not part of the commit;
  delete or clearly label before the phase ends.

## Files changed

- `src/Omskep.Core/Speech/AzureSpeechClient.vb` — added `SynthesizeAsync`;
  `RawResponse`/`SendAsync` now also capture `Retry-After` when present.
- `src/Omskep.Core/Speech/ISpeechClient.vb` — added the `SynthesizeAsync`
  member (added directly by the developer, not drafted by this chat).
- `tests/Omskep.Core.Tests/Support/FakeSpeechClient.vb` — added an optional
  synthesis responder on both constructors (default throws
  `NotImplementedException` so an unintended call fails loudly), plus
  `SynthesisCalls`, `ReturningSynthesis`, `HangingSynthesis`, `OkSynthesis`,
  `FailSynthesis`.
- `README.md` — phase 3 split into 3a/3b in Status; `Documents` and new
  `Export` namespace entries under Project structure.
- `ATTRIBUTIONS.md` — note that phase 3a added no new third-party packages;
  `System.Xml.Linq` and `System.Net.Http` added under Platform components.

## Targets reached

All seven items from the starter prompt's order of work:

1. **Listening checks (a)–(e), done first.** Results, all against the real
   Azure service:
   - (a) straight `'n` vs. the wrong-direction-quote form: both read
     correctly — cosmetic difference only, no sanitizer change needed.
   - (b) Hebrew after Edit > Reverse Hebrew text, read by the Hebrew voice:
     correct order.
   - (c) **Mixed, and decisive.** A bare line break with no sentence-ending
     punctuation nearby reads as a pause/restart, independent of single- vs.
     multi-voice-block structure (isolated across four follow-up spikes,
     c–c4). Resolved by having `SsmlComposer` collapse all whitespace
     (newlines included) to a single space at composition time, never into
     the saved document. This also made the chunker's originally-planned
     "fall back to a line boundary" tier moot (a composed chunk has no
     newlines left to fall back to) — dropped for simplicity.
   - (d) a chunk boundary at an unnatural (mid-sentence) stopping point
     audibly restarts; at a natural one (sentence end or voice switch) it
     does not. Directly shaped the chunker's split-point strategy.
   - (e) no voice-element limit found up to 30 alternating `<voice>`
     elements in one request — the "max 5 voice elements" forum claim does
     not hold here. The chunker has no voice-count cap.
2. **`SynthesizeAsync`** — built, extends the phase 1 `SendAsync`/
   `AzureOutcome` pattern, fixed output format, key fetched per call and
   never retained, no key in any message, own timeout separate from the
   voice-list one.
3. **Composition** — `SsmlComposer`, as listed above.
4. **Chunker** — as listed above. See Deviations for the sentence-end
   heuristic and the duration-estimate fix.
5. **Billable count of composed chunks** — `Chunker.TotalBillableCharacters`,
   pure, callable before any synthesis is attempted (for 3b's export-
   confirmation count).
6. **Export engine** — `ExportEngine`, as listed above.
7. **Resume decision** — same-session only, confirmed by the developer. No
   manifest, no cross-restart state. `ExportResult` carries enough
   (`Chunks`, in order, each with a status) that passing a prior result back
   in as `priorAttempt` resumes from the first chunk that did not finish.

## Deviations from plan

- **Sentence-end heuristic simplified from the starter prompt's own
  suggestion.** Rather than a denylist of known abbreviations (`Joh.`,
  `vgl.`, etc.), the developer's call: use the simple rule (period/question
  mark/exclamation + whitespace + uppercase letter) and rely on the existing
  listen-and-correct workflow to catch any misfire by ear, same as the
  phase 0 pronunciation-correction pattern. If a break sounds wrong at an
  abbreviation, the fix is to write it out (`Mr.` → `mister`) rather than
  teach the chunker about it.
- **Line-boundary fallback dropped entirely**, not just deprioritized — see
  listening check (c) above. Split-point search goes straight from
  sentence-end to voice-switch to reporting, no middle tier.
- **A real estimation bug found and fixed during harness testing**, not
  anticipated in the starter prompt: measuring an inline element (`<sub>`,
  `<phoneme>`, a nested `<prosody>`) by calling `.ToString()` on it while
  still attached to its document tree makes .NET inject a redundant `xmlns`
  declaration (since it inherits its namespace from an ancestor), inflating
  its billable-character estimate by ~40+ characters per occurrence. Fixed
  with a `StripNamespace` helper used only for measurement; the real
  reconstructed output is unaffected (confirmed by a regression test,
  `An_inline_sub_element_is_never_split_across_chunks`, which caught it).
- **The duration-cap sanity check in `ExportEngine` deviates from the
  starter prompt's literal "bytes ÷ 12,000 vs. estimated seconds."** Spike
  testing showed the estimate-vs-actual gap can be large even on ordinary
  content (not just the markup-heavy case the estimate formula already
  accounts for) — a ~900-character, `<voice>`-tag-dense test file estimated
  at ~78s came back as 4.7s of real audio. Using the estimate as a pass/fail
  gate risks failing a perfectly good chunk, so `ExportEngine` instead only
  rejects a response that is implausibly short in an absolute sense (under
  2,000 bytes — likely truncated), regardless of what the estimate said
  going in. **Flagged for explicit developer sign-off**, not yet confirmed
  as of this handoff.
- **An unparseable or keyword `<prosody rate>` (e.g. `"slow"`) is never
  rewritten** — composition and chunking only read it for the internal
  duration estimate (falling back to a fixed conservative multiplier, ~1.55,
  matching the phase 0 measurement of `"slow"`) and always copy the
  attribute through verbatim to the output. Explicit developer decision,
  confirmed by a regression test exercising the reconstructed-split path
  specifically (`A_keyword_rate_survives_a_reconstructed_split_verbatim`).
- **Chunker failure messages report a text excerpt, not a line number.**
  Originally implemented with `IXmlLineInfo`, then found to be worthless in
  practice: `SsmlComposer` removes every newline, so a composed document is
  always exactly one XML line — every failure would have said "line 1"
  regardless of where the actual problem was. Replaced with a ~60-character
  excerpt of the offending text, searchable in the editor. Caught and fixed
  via the harness happy-path check itself (see Verification outcome).

## Known issues or deferred items

- **The `ExportEngine` sanity-check deviation above needs the developer's
  explicit sign-off** before being relied on for a real export. If a
  tighter check is wanted, the only robust basis identified is comparing
  against `BillableCharacterCounter`'s count directly (not the duration
  estimate) — not implemented, pending that decision.
- **`priorAttempt` resume matching is by chunk count and position only**, no
  content identity check (e.g. hashing each chunk's SSML). If a document
  were edited and re-composed/re-chunked to the same chunk count between
  attempts, old audio could be wrongly treated as reusable. Believed safe in
  practice (3b's UI would only ever pass back a result from the export
  currently being retried) but not enforced by the engine itself.
- **A chunk-synthesis failure after the final `AtomicFile.WriteAllBytes`
  call throws** still returns `ExportStatus.FailedResumable`, even though
  every chunk already succeeded and nothing needs re-synthesizing — only
  the write needs retrying. 3b's UI should know this distinction exists
  when writing user-facing messaging (a disk-full/permissions error, not a
  "some audio failed" error).
- **Sentence-end detection is local to one text node** — a sentence that
  happens to end immediately before an inline element (e.g. right before a
  `<sub>`) is not detected as a split point there; the pieces simply stay
  packed together unless the cap forces a split elsewhere nearby. Documented
  simplification, not expected to matter in practice given the 480s cap's
  margin over any real sentence length.
- **Export's `AtomicFile` usage is the byte-array `WriteAllBytes` overload**,
  holding the full joined audio in memory before the atomic swap. The
  starter prompt flagged that "a file-to-file variant... may be needed for
  large audio" — not built. At realistic sizes (the largest known study,
  ~80,600 characters, is well under an hour of audio even at Azure's own
  bitrate) this comfortably fits in memory; worth revisiting only if a much
  larger document is ever expected.
- **No new unit tests were written for `ExportEngine` or
  `AzureSpeechClient.SynthesizeAsync` directly** — only `SsmlComposer` and
  `Chunker` have MSTest coverage from this phase. The starter prompt's
  listed hostile-input set for the export engine (401/429/5xx/timeout/
  offline/cut-off-mid-export, cancel-and-resume, no-key-in-any-message,
  etc.) is still outstanding.

## Verification outcome

1. **Compiles with 0 warnings/errors; all tests pass** — confirmed by the
   developer (466/466 after the namespace-stripping fix; a further 2 tests
   for `TotalBillableCharacters` added since but not yet run).
2. **App still launches, phases 1 and 2 behave as before** — not
   independently re-verified this phase (no app-facing change was made);
   worth a quick check before phase 3b given it touches `AppSession`.
3. **Listening checks (a)–(e)** — done, see Targets reached above.
4. **Harness happy path** — done twice against the real Azure service via
   `Spike.Pipeline`: once with concatenated whole-`<voice>` chunks (free
   split), once with a single oversized `<voice>` block forced to split at
   sentence ends (reconstructed split, after correcting the test's
   artificially small cap — see Deviations). Developer listened at every
   seam in both; "everything sounded perfect."
5. **Must-never checks by test** — partially covered: no nested `<voice>`
   ever composed (tested), structural correctness of a reconstructed split
   around an inline element (tested, `An_inline_sub_element_is_never_split
   _across_chunks`). **Not yet covered by test**: no export without the
   composed count available, no request over the duration cap by estimate
   reaching Azure, no final file under its real name after a failed chunk,
   no key in any message — these apply to `ExportEngine`, which has no unit
   tests yet (see Known issues).
6. **Edge spot-checks** — not yet done against `ExportEngine` (a mid-export
   429/offline with resume; a document with an element spanning a chunk
   boundary was implicitly covered by the reconstructed-split harness run).
7. **Placeholder audit** — `MainForm.mnuExport_Click` remains the single
   documented placeholder, as expected; nothing new added.

## Suggested coordinator update

- **For phase 3b:** the engine's public surface is `SsmlComposer.Compose`,
  `Chunker.Chunk` / `Chunker.TotalBillableCharacters`, and
  `ExportEngine.ExportAsync` (constructor: `ISpeechClient`, `ISecretStore`,
  `AppSession`, `ConnectionMonitor`; call signature: chunks, output path,
  cancellation token, optional `IProgress(Of ExportProgress)`, optional
  `priorAttempt As ExportResult` for same-session resume). 3b needs to: (a)
  decide and confirm the `ExportEngine` sanity-check approach (see Deviations
  and Known issues above) before relying on it; (b) write the
  `ExportEngine`/`SynthesizeAsync` hostile-input tests the starter prompt
  called for, which did not get written this phase; (c) wire `ExportProgress`
  (states: `Sending`, `Retrying`, with chunk index/total/retry count) to
  whatever progress UI it builds; (d) decide user-facing wording for the
  "every chunk succeeded, only the write failed" case called out above.
- **For phase 4 (voice authoring):** the nested-`<voice>` rule is enforced
  unconditionally with no escape hatch — the developer explicitly declined
  to explore whether nested `<voice>` might help a single-word
  mixed-language case (e.g. one Hebrew word inside an English sentence),
  specifically to avoid depending on undocumented Azure behaviour that could
  change. Phase 4's voice-authoring UI should not offer or imply that nesting
  voices is possible. Also worth carrying forward: voice-element count has
  no practical limit (tested to 30 in one request), so the reference panel
  need not warn about or limit how many voice switches a document uses.

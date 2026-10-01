# Omskep — Phase 0 handoff

Phase 0 ran over two chats (scaffold and decisions 9 and 10 in the first; the
Azure, SSML, ID3 and editor spikes in the second). All spike code was
throwaway and is deleted; only the decisions, the evidence below, and the
scaffold carry forward.

## 1. Phase goal

Settle the ten phase 0 decisions with evidence, run the spikes needed to do so
on the developer's own machine, and leave a compiling scaffold with README,
ATTRIBUTIONS, LICENSE and .gitignore.

## 2. Decision summary

| # | Decision | Outcome |
| --- | --- | --- |
| 1 | Chunking and Azure limits | Limit is audio **duration** (10 min documented), not characters. Chunk by estimated duration; join chunks by byte concatenation. |
| 2 | Billable characters | Display a guaranteed **upper bound**. Azure bills markup except `<speak>`/`<voice>` tags and bills some whitespace less. |
| 3 | API key storage | DPAPI (current user) blob in `%LOCALAPPDATA%\Omskep\settings.json`; `ISecretStore` abstraction; first-run lockout. |
| 4 | ID3 fields | TagLibSharp2; field defaults agreed and verified in foobar2000 and Explorer. |
| 5 | Export naming | `<document> - alphaN.mp3` and `<document> - RCN.mp3`, auto-incremented, never silently overwritten. |
| 6 | Editor control | **Scintilla5.NET 7.0.0** behind a wrapper in `Omskep.App`. |
| 7 | SSML capabilities | Voice switching, breaks, `<sub alias>`, IPA `<phoneme>`, prosody rate verified. Latin unsupported (no voice). |
| 8 | Voice list source | Fetched at runtime from the user's key and region; no built-in list. |
| 9 | Project structure | Done in the first chat (see section 4). |
| 10 | Dependency licenses | Checked. **Azure Speech SDK dropped in favour of the REST API.** |

## 3. Decisions with evidence

All tests below were run by the developer on their own machine (Windows,
Visual Studio 2026, .NET 10 SDK 10.0.401) against the DashingDogSpeechService
resource (South Africa North, Free F0) with `af-ZA-AdriNeural` unless stated.

### D1 — Azure limits, chunking and joining

**Findings**

- Documentation: real-time synthesis limits audio length to 10 minutes per
  request. No character cap was found for real-time synthesis (the 3,000 and
  20,000 character figures belong to avatar/batch rows).
- Speaking speed: short test sentences 12.4 characters/s; real study prose
  15.2 characters/s (2,992 characters gave 197.5 s) and 15.5 characters/s
  (10,496 characters gave 675.7 s).
- A 10,496-character request produced **11:15.7** of audio, complete, over
  both the SDK and REST. Azure evidently has headroom beyond 10 minutes here,
  but the documented limit remains the contract and must not be relied on.
- One 4,000-character SDK request returned `Canceled` for an unknown reason and
  succeeded on retry; it did not recur.
- Chunk files from Azure are headerless 96 kbit/s CBR MP3 frame streams
  (first bytes `FF F3 A4 C4`; no ID3, no Xing/Info frame, no ID3v1 footer).
- Joining three chunks (1,223 + 1,428 + 1,372 characters) by plain byte
  concatenation gave a file of 4:21.218 in foobar2000, against 4:21.122 for the
  same text as a single request: +0.05 s per seam (about one 48 ms frame). No
  clicks, gaps or tone change were audible at the seams.
- Real-time rate: 11 minutes of audio took 37 s (SDK) and 51 s (REST).

**Chosen**

1. Estimate chunk duration as `characters / (10 × (1 + rate))` seconds, where
   `rate` is the prosody rate (for example -0.10) and 10 characters/s is
   deliberately below the slowest measured speed. Cap at **480 s** (20%
   headroom under the documented 10 minutes). At the default rate this is about
   4,800 characters per chunk; at `-10%` about 4,300; at `-30%` about 3,400.
   The largest known study (about 80,600 characters) needs roughly 17 to 19
   chunks.
2. Split only between top-level blocks, never inside an element. Each chunk is
   a standalone SSML document with its own `<speak>` and the `<voice>` wrapper
   of the block it came from.
3. Use one fixed output format for every chunk (`audio-24khz-96kbitrate-mono-mp3`);
   byte concatenation is only valid if all chunks match. Guard with a test.
4. Synthesize each chunk to its own temporary file; write the final MP3 under
   its real name only when every chunk succeeded (join, then stamp ID3, then
   atomic rename).
5. Retry a failed chunk a small fixed number of times with a short pause. Keep
   finished chunk files so a failed export can resume. Tell the user plainly
   what finished, what remains, and that a retry may bill the chunk twice.
6. After every chunk, check the HTTP result and that the returned audio length
   is plausible for the characters sent (bytes ÷ 12,000 ≈ seconds).

**Rejected:** a fixed character limit (not the real constraint); relying on the
observed headroom beyond 10 minutes; re-encoding at seams (unnecessary).

### D2 — Billable characters

**Method:** small SSML requests; the resource's *Synthesized Characters (Sum)*
metric was read at 1-minute granularity.

| Test | Content | Counter value (all characters except `<speak>`/`<voice>` tags, CRLF = 1) | Azure billed |
| --- | --- | --- | --- |
| Hello | 39 characters of text | 39 | 39 |
| W1 | two sentences, one space between | 57 | 57 |
| W2 | six spaces between | 62 | 62 |
| W3 | CRLF between | 57 | 57 |
| W4 | CRLF + 4 spaces between | 61 | 61 |
| W5 | leading and trailing whitespace around the text | 36 | 28 |
| W6 | whitespace-only gap between two `<break>` tags | 75 | 70 |
| P1 | two sentences with CRLF + 6 spaces, inside `<prosody>` | 94 | 88 |
| P2 | space after a `<break>` tag | 78 | 77 |
| P3 | three sentences, two CRLF + 6-space separators, no wrapper | 98 | 98 |

An earlier 3-call run (plain, `<break>`/`<prosody>` markup, pretty-printed)
billed about 1,560 characters in total; counting every character gave 2,065
and counting text only gave 1,364, so markup counts.

**Observed model (not guaranteed):** `<speak>` and `<voice>` tags are free;
other tags count character for character; text counts, with a CRLF as one
character; whitespace at the edges, next to a tag, or alone between tags is
free; inside `<prosody>`, a whitespace run seems to count as one.

**Chosen:** the pre-export display is an **upper bound**, labelled "up to N
characters": count every character except the `<speak>`/`<voice>` tags, with
CRLF as one. It can only equal or exceed the real bill. It must be computed on
the **final composed chunk SSML**, including anything the app adds (prosody
wrappers, a disclosure line). Unit tests use the table above as fixed vectors:
counter ≥ billed in every row, and equal in Hello, W1 to W4 and P3.

**Not tested:** how `&amp;` and other entities are billed (counted as written
is safe); whether the voice-list call consumes characters (not checked, and it
is not a regular call).

**Rejected:** modelling Azure's exact whitespace rules (maintaining a guess
about its internals); "text only" counting (undercounts).

### D3 — API key storage and first-run UX

**Evidence**

- DPAPI (`ProtectedData`, current-user scope, fixed application entropy)
  round trip equalled the original key; the settings file did not contain the
  plaintext key; it worked from `%LOCALAPPDATA%` although the developer's
  Windows profile path contains an apostrophe.
- A corrupted blob threw `CryptographicException` ("The data is invalid.").
- `ProtectedData` needs `net10.0-windows` (analyzer CA1416 otherwise).
- Over the SDK: a made-up key and a wrong region gave the same 401
  `AuthenticationFailure`; offline gave `ConnectionFailure`; failures take
  under 1.5 s. Over REST: 401 with an empty body for both bad key and wrong
  region. The error text never contained the key.

**Chosen**

- Key stored as a DPAPI blob (base64) in `%LOCALAPPDATA%\Omskep\settings.json`
  written atomically (temp file then replace). Region, default voices and
  per-language speed presets are plain text in the same file.
- `ISecretStore` (save, load, clear) in `Omskep.Core`; the DPAPI implementation
  lives in `Omskep.App`; tests use an in-memory fake.
- Key never logged, never shown after entry (only "Key saved", with Replace and
  Clear), never in error messages.
- Branches: no file or no key means everything locked except Settings; blob
  fails to decrypt means treat as no key with a plain message; key present and
  voice cache present means unlocked; key present, no cache, offline means
  locked with a connection message; Azure rejects the key means keep the cache
  and editing, block export, point to Settings; never delete the key
  automatically.
- One combined message: "Azure rejected the key or region. Check both."
- Settings page: masked key box, editable region drop-down of common regions,
  "Test connection" (fetches the voice list).

**Rejected:** plaintext settings; environment variables (spike only);
Credential Manager (more code, little gain here).

### D4 — ID3 fields (TagLibSharp2)

**Evidence:** a copy of the joined MP3 was stamped with Unicode-heavy values,
reopened, and checked in foobar2000 and Windows Explorer. All fields showed
correctly (including ø, ê, ë, ï and a Hebrew word); tag type ID3v2.4; the tag
added 1,300 bytes; the original audio bytes were identical at the end of the
file; duration unchanged at 4:21.218 and the audio played normally.

**Chosen defaults**

| Field | Default | Editable per export |
| --- | --- | --- |
| Title | Document name, plus a pass suffix such as `[RC1]` on non-first passes | Yes |
| Artist | Author from PDF metadata if present, else blank | Yes |
| Album | Series or group name, remembered from the last export | Yes |
| Track | Leading number in the filename (the `2` in `2_Universe`), if any | Yes |
| Year | Export date | No |
| Genre | Audiobook | No |
| Comment | Pass label, voice, rate, "Made with Omskep", source file | Pass label |

Proposed addition: the Comment also states that the voice is synthetic
(Azure AI Speech). The pass label appears in both Title and Comment so renamed
or similarly named files stay distinguishable.

**TagLibSharp2 API notes (0.6.0, MIT):** read with `Mp3File.ReadFromFile(path)`
and take `.File`; `Year` is a **string**; `Track` accepts a number;
`SaveToFile(path, originalBytes)`; the library's own duration estimate
(4:21.240) differs slightly from foobar2000's, so compute duration from file
size. It is a young 0.x library with little real-world use; original TagLibSharp
(LGPL-2.1) remains the fallback.

**Open:** ID3v2.4 is fine in foobar2000 and Explorer, but older phone or car
players may prefer v2.3. Not tested; phase 5 item.

### D5 — Export naming

**Chosen:** `<document name> - alphaN.mp3` for first passes and
`<document name> - RCN.mp3` for Release Candidates, with no space before the
number. N is the next free number for that stem in the output folder. The save
dialog is prefilled; overwriting requires typing an existing name and an
explicit confirmation. Illegal Windows filename characters are replaced and the
full path is checked against the length limit. The "next free name" logic is a
pure function in `Omskep.Core` with unit tests (empty folder, gaps, mixed
alpha and RC files, illegal characters, very long names, existing exact name).

### D6 — Editor control

**Evidence**

- Package: **`Scintilla5.NET` 7.0.0**, released July 2026, MIT, lists
  compatibility with net8.0-windows (usable from `net10.0-windows`). Several
  older packages with similar names (`Scintilla.NET`, `ScintillaNET`) are
  deprecated or stale; use only the `Scintilla5.NET` ID. Single maintainer.
- Spike on the real document wrapped in SSML (80,637 characters, 1,335 lines):
  load 7 to 14 ms; no lag in fast scrolling, typing, resizing or undo (undo
  grouping felt like Word).
- Line-number margin works; a wrapped paragraph keeps one line number; numbers
  below an inserted line shift correctly.
- XML colorization via `LexerName = "xml"` and `Style.Xml.*` styles works.
- Live well-formedness check (400 ms debounce, `XmlReader`, DTDs prohibited):
  1 ms on 80,000 characters. Squiggle indicators work; tested errors: stray
  `<oops`, mismatched `</voic>`, lone `&`, missing `</speak>` (no place for a
  squiggle, so the status bar message and a "Go to error" button are needed).
  The parser reports where it notices a problem, which can be a line after the
  cause, and only the first error.
- Hebrew renders in the correct order in both default and DirectWrite modes;
  DirectWrite looks slightly nicer. Caret movement and Backspace in Hebrew are
  slightly unusual (accepted: Hebrew will rarely be hand-edited).

**Chosen:** Scintilla5.NET, DirectWrite as the default rendering mode with a
setting to turn it off (untested over Remote Desktop or on unusual graphics
hardware), wrapped by a small class in `Omskep.App` so it can be replaced.

**Rejected:** stock `RichTextBox` (no line numbers, no indicators, manual slow
colorization).

### D7 — SSML behaviour

- **Voices on this resource** (556 voices, 153 locales): Afrikaans 2
  (`af-ZA-AdriNeural`, `af-ZA-WillemNeural`), en-ZA 2, en-US 58, en-GB 17,
  en-AU 15, en-NZ 2, Hebrew 2 (`he-IL-AvriNeural`, `he-IL-HilaNeural`), Greek 2
  (`el-GR-AthinaNeural`, `el-GR-NestorasNeural`), **Latin 0**.
- **Voice switching** with `<voice name>` wrapping inside one document is
  seamless; each language is a distinct, recognisable speaker.
- **Hebrew** with and without vowel points sounded identical. Default speed
  was far too fast; `<prosody rate="-30%">` made it easier to follow.
  Greek (polytonic and monotonic) is recognisable on the modern Greek voice;
  quality is for the developer to judge in use.
- **Rate** scales duration by about 1/(1+rate): -20% gave ×1.25, -30% gave
  ×1.43, `slow` gave ×1.55 (he-IL-HilaNeural).
- **Breaks:** `<break>` adds its nominal time on top of the normal sentence
  pause (about 1.1 s after a period and space); 500 ms and 1 s tested exactly;
  6 s honoured (no clamp at 5 s). `strength` keywords behave as roughly 500,
  750, 1,000 and 1,250 ms (weak, medium, strong, x-strong), derived from the
  data. A newline between sentences gives the same pause as a space.
- **`<p>` and `<s>`** add no pause compared with plain text, so the sanitizer
  need not wrap paragraphs.
- **Pronunciation of "Job"** (read like English by default): `<sub alias="Jop">`
  sounded best; IPA `<phoneme alphabet="ipa" ph="jɔp">` sounded correct;
  respelling and other IPA variants differed audibly from the baseline. The
  SAPI alphabet was accepted without an error but not judged by ear, so only
  document IPA.
- Measured audio durations come from MP3 frames of about 48 ms, so tiny
  differences are invisible; listening is the real test.
- **Latin:** no voice on this resource. Documented as unsupported until a real
  phrase needs reading; whether another voice reads Latin acceptably was not
  tested.

### D8 — Voice list source

**Chosen:** fetch from the user's own key and region at first use; cache in a
local file; refresh on key change and via a "Refresh voices" button; group by
locale in the UI. **No built-in starter list**: before a key is entered, all
features except Settings are disabled (the editor is useless without audio, and
a bundled list would go stale if Microsoft changes the Afrikaans voices). The
list call works on the free tier (SDK `GetVoicesAsync` and REST
`/cognitiveservices/voices/list`, about 430,000 characters of JSON). Whether it
is billable was not checked.

### D9 — Project structure (first chat)

Repo `C:\source\Omskep`, GitHub `asherbendavid/Omskep`, branch `main`:
`src/Omskep.Core` (class library, all Critical logic, no UI or platform
dependencies), `src/Omskep.App` (WinForms, `net10.0-windows`),
`tests/Omskep.Core.Tests` (MSTest), `spike/` (throwaway). Root
`Directory.Build.props`: Option Strict, Explicit and Infer On,
`TreatWarningsAsErrors`. MSTest chosen over xUnit on learning-curve grounds.
Solution file `Omskep.slnx`. Additions from this phase: `ISecretStore` and
pure naming, counting and chunking logic belong in Core; the DPAPI store and
the Scintilla wrapper belong in App. The REST client uses `HttpClient`, which
is platform-neutral, so it can live in Core behind an interface with a fake
`HttpMessageHandler` for tests (coordinator to confirm).

### D10 — Licenses

| Component | License | Verdict |
| --- | --- | --- |
| PdfPig | Apache-2.0 (derived from Apache PDFBox) | Compatible; ship license text and any NOTICE |
| TagLibSharp2 0.6.0 | MIT | Compatible |
| Scintilla5.NET 7.0.0 | MIT | Compatible |
| Scintilla and Lexilla (native) | Permissive notice, Copyright 1998-2021 Neil Hodgson | Compatible; the notice must accompany copies |
| MSTest (test only) | MIT, to confirm when pinned | Not distributed |
| Azure Speech SDK | Proprietary Microsoft license terms | **Not used** |
| TagLibSharp (original) | LGPL-2.1 (from the earlier notes) | Fallback only |

**Azure Speech SDK:** the license permits use for developing and testing, but
distribution is limited to files on its REDIST.TXT list, and requires adding
significant functionality, requiring end users to accept terms that protect the
code and Microsoft, and indemnifying Microsoft. Distributing a built Omskep
bundled with the SDK to the study group would trigger those conditions.
REST was therefore tested as a replacement.

**REST evidence (South Africa North, no SDK):** POST to
`https://{region}.tts.speech.microsoft.com/cognitiveservices/v1` with headers
`Ocp-Apim-Subscription-Key`, `X-Microsoft-OutputFormat:
audio-24khz-96kbitrate-mono-mp3` and content type `application/ssml+xml`
returned HTTP 200 and a correct MP3 with the same frame header as the SDK;
10,496 characters returned HTTP 200 with 8,109,216 bytes (11:15.7) in 51 s;
bad key and wrong region returned 401 with an empty body; the voice list
returned HTTP 200. **Chosen: REST via `HttpClient`, no SDK.**

**Service and output terms (open, developer reading):** the SDK license is
about software, not about the service or the audio. A Microsoft Q&A answer
quoting the Azure Product Terms says commercial use of prebuilt-voice output is
granted to paid-tier customers; the free tier is described as evaluation. The
developer's use is non-commercial, so the wording is unclear for sharing.
Microsoft also recommends disclosing that the voice is synthetic. The developer
intends to use a paid resource for distributed audio once the study leader
approves, and wants that intent in the README (done). Ballpark price from
third-party summaries: about US$16 per million characters (about US$1.30 for an
80,000-character study); the official page was not opened directly.

**Study leader permission:** the leader (author of the documents) agreed by
phone to a one-document trial; if he objects, Omskep stays personal use only.

## 4. Files added and changed

**Added or replaced in the repository**

- `README.md` (replaced): status, planned workflow, Azure cost and responsible
  use, known limitations, build instructions, structure.
- `ATTRIBUTIONS.md` (replaced): confirmed licenses, services, rejected options,
  pre-release checklist.
- `docs/omskep-phase0-handoff.md` (this file; suggested location).

**Changed:** `.gitignore`. Replace the last two lines
(`# testdata for spike tests not included in commits` and
`spike/Spike.Azure/testdata/`) with:

```
# Real study PDFs, extracted text, generated audio and local secrets
# must never be committed
**/testdata/
*.extracted.txt
*.mp3
secrets.json
*.secrets.json
```

**Created in the first chat (scaffold):** `Omskep.slnx`,
`src/Omskep.Core`, `src/Omskep.App`, `tests/Omskep.Core.Tests`,
`Directory.Build.props`, `LICENSE` (BSD-3-Clause, Chris van Coeverden),
`.gitignore`, first `README.md` and `ATTRIBUTIONS.md`.

**Throwaway, never committed, to delete locally:** `spike/Spike.Azure`
(Azure SDK/REST, billing, voice, chunk-join, ID3 and DPAPI spikes) and
`spike/Spike.Editor` (Scintilla spike), plus
`%LOCALAPPDATA%\OmskepSpike` if it still exists.

## 5. Targets reached

All ten decisions are recorded with evidence. Not reached as originally
phrased: decision 1 as "per-request character limit" (reframed to duration)
and decision 10 as "record the Azure Speech SDK license" (the SDK is not used).
Latin (part of decision 7) is parked by choice.

## 6. Deviations from plan

1. **Azure Speech SDK replaced by REST** (license redistribution conditions,
   clean HTTP status codes, no 50 MB native package).
2. **Chunk limit is duration, not characters.**
3. **TagLibSharp2 chosen** over original TagLibSharp (MIT versus LGPL-2.1).
4. **No built-in voice list**; first-run lockout instead of a starter list.
5. **Editor package is `Scintilla5.NET`**, not the older ScintillaNET.
6. **Latin deferred** until a real phrase needs reading.
7. Phase 0 needed two chats instead of one.
8. New requirement discovered: saved per-language pronunciation replacement
   list (see section 9).

## 7. Known issues and deferred items

- Latin unsupported (no voice); other voices not tried on Latin.
- 429 (quota), offline, and 5xx behaviour over REST are untested; design by
  status code and exception type, test with a fake handler.
- Billing of XML entities (`&amp;`) and of the voice-list call is unknown.
- Behaviour beyond the documented 10-minute limit is undocumented; never
  depend on it. REST documentation describes its use cases as limited.
- One unexplained `Canceled` SDK response; assume transient failures happen.
- TagLibSharp2 is 0.x; Scintilla5.NET has a single maintainer. Both are behind
  wrappers.
- ID3v2.4 compatibility on older players untested (phase 5).
- Output-rights wording for free-tier audio shared with others; the developer
  is reading the primary terms.
- Study leader's approval after the trial document is pending.
- DirectWrite untested over Remote Desktop or odd graphics hardware.
- The SAPI phoneme alphabet is unverified by ear.
- Extraction: `page.Text` gave 80,449 characters and `ContentOrderTextExtractor`
  81,606 (both 65,229 non-whitespace); choose in phase 1. The test PDF is
  encrypted with a copy-denied permission flag and PdfPig still read it.
  Only one PDF has been tested.
- PDF extraction artefacts seen: wide runs of spaces in running headers,
  private-use glyphs U+F098 and U+F099 (symbol-font bullets), U+2022 bullets,
  em and en dashes, curly quotes, U+2212, U+2026, Hebrew letters including the
  presentation form U+FB4B, and 22 occurrences of `‘n` (Word's wrong-direction
  quote in the Afrikaans article).

## 8. Verification outcome

Adapted from `workflow.md`:

| Check | Result |
| --- | --- |
| Every spike result reproduced by the developer on their own machine | Yes |
| Solution compiles with 0 warnings and errors | Yes at scaffold; **re-run after cleanup** |
| App launches an empty main form | **Developer to confirm after cleanup** |
| Test project runs | **Developer to confirm after cleanup** |
| No spike or placeholder code in the shipping projects | To confirm: spike folders deleted, `src` unchanged |
| Must-never and edge-case checks | N/A this phase (no shipping logic); inputs for them are in sections 3 and 9 |

Cleanup checklist (from `C:\source\Omskep`): delete `spike\Spike.Azure` and
`spike\Spike.Editor`; `dotnet build` (expect 0 warnings and errors);
`dotnet test`; run `Omskep.App` once; `git status` should show only the docs
and `.gitignore` changes. Suggested commits: `docs: update README and
ATTRIBUTIONS for phase 0 decisions`, `chore: ignore test data, audio and
secrets`, `docs: add phase 0 handoff`; then `git tag phase-0`.

## 9. Suggested coordinator update

### Requirements document (`omskep-requirements.md`)

1. **Key dependencies:** remove "Azure Cognitive Services Speech SDK". Add
   "Azure AI Speech REST API via `HttpClient`". Replace TagLibSharp with
   TagLibSharp2 0.6.0 (MIT). Add Scintilla5.NET 7.0.0. Keep PdfPig.
2. **Must-never list:** change "silently exceeds Azure's per-request character
   limit" to "silently exceeds the per-request audio duration limit (10
   minutes documented)". Reword the billable-character item: markup counts
   except `<speak>`/`<voice>` tags, and the displayed count is an upper bound.
3. **Open phase-0 decisions section:** replace with the settled decisions in
   section 2.
4. **Edge cases to add:** copy-denied/encrypted PDFs that PdfPig still reads;
   private-use bullet glyphs; `‘n` wrong-direction quote; Hebrew presentation
   forms (normalize with NFC; to be proved by unit test); wide whitespace runs
   in headers; transient failed chunk (retry and resume); HTTP statuses 401,
   429, 5xx and no network; PDF author metadata missing.
5. **New requirements:**
   - Saved, user-editable pronunciation replacement list per language
     (find → preferred markup, `<sub alias>` first), applied by an explicit
     bulk command with preview and single undo. Replace only inside text
     nodes (never in tags or attributes), whole word and case sensitive by
     default, skip text already inside `<sub>` or `<phoneme>`, validate
     well-formedness after applying, scope each list to the language of its
     enclosing `<voice>`. Lists can be exported and imported. Tier: Important,
     with the text-node-only and well-formedness rules treated as Critical.
   - Per-language default speaking-rate presets, applied as `<prosody rate>`
     inside each `<voice>` when chunks are built.
   - First-run lockout and voice-list cache (decisions 3 and 8).
   - Synthetic-voice disclosure in the ID3 comment and README; paid-use intent
     in the README.
6. **Platform:** App targets `net10.0-windows`; Core stays platform-free.
7. **README limitations:** Latin unsupported; block-level language tagging;
   Greek read by a modern Greek voice.

### Later phase prompts

- **New early phase (suggested "Phase 1a: Settings and Azure connection"),
  or fold into phase 1:** Settings page, `ISecretStore` and DPAPI store,
  atomic settings file, REST client with status-code mapping, "Test
  connection", voice-list cache and refresh, first-run lockout. This must
  exist before export and gates the other features.
- **Phase 1 (editor):** use `Scintilla5.NET` only; `LexerName = "xml"` and
  `Style.Xml.*`; number margin; word wrap; `Indicators` squiggle with
  `IndicatorFillRange`; DirectWrite setting; 400 ms debounced `XmlReader`
  well-formedness check (DTDs prohibited, first error only, show the parser's
  message and a "Go to error" action). Sanitizer rules: escape only `&`, `<`,
  `>`; strip private-use characters; NFC normalize; fix `‘n`; collapse
  whitespace runs; LF line endings; no `<p>` wrapping needed. Choose between
  `page.Text` and `ContentOrderTextExtractor`. Detect no-text PDFs.
- **Phase 2 (export):** REST synthesis; chunk formula and 480 s cap; temporary
  chunk files, retry and resume; byte-concatenation join; TagLibSharp2 API
  notes; naming function; counter with the vectors in section 3 (computed on
  final chunk SSML including wrappers); 429 and offline handling by status
  and exception type; output folder and overwrite confirmation.
- **Phase 3 (voices and reference panel):** per-language rate presets
  (Hebrew -30% was judged better); reference panel content from D7 (breaks
  and strength values, newline versus break, `<sub alias>` first, IPA only,
  `<p>`/`<s>` unnecessary).
- **Phase 4 (preview and corrections):** pronunciation list feature above;
  "Job" → "Jop" as the first test case.
- **Phase 5 (polish):** ID3v2.3 compatibility check; Remote Desktop and
  DirectWrite check; license texts and NOTICE for the first binary release;
  final README and ATTRIBUTIONS review; read the primary Azure terms again
  before wider distribution.

## Appendix — proven snippets for later phases

REST request (tested):

```vb
Using req As New HttpRequestMessage(HttpMethod.Post,
        $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1")
    req.Headers.Add("Ocp-Apim-Subscription-Key", key)
    req.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-96kbitrate-mono-mp3")
    req.Headers.Add("User-Agent", "Omskep")
    req.Content = New StringContent(ssml, Encoding.UTF8, "application/ssml+xml")
    Using resp As HttpResponseMessage = Await http.SendAsync(req)
        Dim bytes As Byte() = Await resp.Content.ReadAsByteArrayAsync()
        ' resp.StatusCode: 200 ok, 401 key or region rejected
    End Using
End Using
```

DPAPI (needs `net10.0-windows` and the `System.Security.Cryptography.ProtectedData` package):

```vb
Dim blob As Byte() = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(key), entropy, DataProtectionScope.CurrentUser)
Dim key As String = Encoding.UTF8.GetString(
    ProtectedData.Unprotect(blob, entropy, DataProtectionScope.CurrentUser))
```

TagLibSharp2 (`Imports TagLibSharp2.Mpeg`):

```vb
Dim mp3 = Mp3File.ReadFromFile(path).File
mp3.Title = title : mp3.Artist = artist : mp3.Album = album
mp3.Track = 2 : mp3.Year = "2026" : mp3.Genre = "Audiobook" : mp3.Comment = comment
mp3.SaveToFile(path, File.ReadAllBytes(path))
```

Scintilla5.NET (`Imports ScintillaNET`; note `Style.[Default]` needs brackets in VB):

```vb
editor.StyleResetDefault()
editor.Styles(Style.[Default]).Font = "Consolas"
editor.StyleClearAll()
editor.LexerName = "xml"
editor.Styles(Style.Xml.Tag).ForeColor = Color.Blue
editor.Margins(0).Type = MarginType.Number
editor.Indicators(0).Style = IndicatorStyle.Squiggle
editor.IndicatorCurrent = 0
editor.IndicatorFillRange(position, length)
```

Well-formedness check:

```vb
Dim settings As New XmlReaderSettings() With {.DtdProcessing = DtdProcessing.Prohibit}
Using reader As XmlReader = XmlReader.Create(New StringReader(docText), settings)
    While reader.Read()
    End While
End Using
' catch XmlException: ex.LineNumber, ex.LinePosition, ex.Message
```

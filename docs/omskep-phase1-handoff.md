# Omskep: Phase 1 handoff (Settings and Azure connection)

Suggested location: `docs/omskep-phase1-handoff.md`. Phase 1 ran in one chat
over eight steps. Test counts below are from the developer's own Visual Studio
run (229 passed, 0 failed).

## 1. Phase goal

Take the app from an empty shell to "I can enter my Azure key and the app tells
me it works", with the app locked until it does. Concretely: settings storage,
key protection, the Azure REST client, the voice list and its cache, the
app-wide lockout state machine, and the Settings and main windows. Nothing from
the editor, import, export, voice assignment or pronunciation phases was
started.

## 2. Files added

Paths are relative to the solution root.

### `src/Omskep.Core` (platform-neutral, all unit tested)

| File | Purpose |
| --- | --- |
| `Access/AccessTypes.vb` | Enums (`KeyState`, `CacheStatus`, `AzureOutcome`, `AccessMode`, `AccessReason`), `AccessState`, `ConnectionSnapshot`, all lockout messages |
| `Access/AccessEvaluator.vb` | Pure lockout state machine, HTTP-status classification, `ConnectionMonitor` (sticky key rejection) |
| `Storage/AtomicFile.vb` | Crash-safe write (temp file, flush, swap) with bounded retry for transient Windows file locks |
| `Settings/AppSettings.vb` | `settings.json` schema v1 |
| `Settings/SettingsStore.vb` | Load (with read retry, damaged-file set-aside) and atomic `Save` / `Update` |
| `Secrets/ISecretStore.vb` | `ISecretProtector`, `ISecretStore`, `SecretLoadResult` |
| `Secrets/FileSecretStore.vb` | Key stored as a protected blob inside `settings.json` |
| `Secrets/KeyRules.vb` | What counts as a usable key (shared by store and session) |
| `Speech/AzureRegion.vb` | Strict region validation and the common-region list |
| `Speech/ISpeechClient.vb` | Client interface and `VoiceListResult` |
| `Speech/AzureSpeechClient.vb` | `HttpClient` wrapper with one shared, classifying `SendAsync` |
| `Speech/VoiceListParser.vb` | Reads only the voice fields we keep |
| `Speech/AzureHttpClientFactory.vb` | Shared `HttpClient`, redirects off, infinite client timeout |
| `Voices/VoiceCache.vb` | `VoiceInfo`, `VoiceCache` and `VoiceCacheStore` (`voices-cache.json`) |
| `Voices/VoiceCatalog.vb` | Voices grouped by locale |
| `Voices/VoiceService.vb` | Fetch, cache and monitor orchestration (`TestAsync`, `RefreshAsync`, `Adopt`) |
| `Session/AppSession.vb` | App-wide brain behind both windows; owns the save policy |
| `Session/ConnectionMessages.vb`, `ActionResult.vb` | User-facing text and action results |
| `Session/FormStates.vb` | `SettingsFormState` and `MainFormState`, pure UI rules |

### `src/Omskep.App` (Windows only)

| File | Purpose |
| --- | --- |
| `Secrets/DpapiSecretProtector.vb` | Windows DPAPI, current-user scope, fixed entropy |
| `MainForm.vb`, `MainForm.Designer.vb` | Main window: menu, lock banner, status bar (replaces `Form1`) |
| `SettingsForm.vb`, `SettingsForm.Designer.vb` | Settings dialog |

### Tests

- `tests/Omskep.Core.Tests` (222 tests): `Access`, `Secrets`, `Session`,
  `Settings`, `Speech`, `Storage`, `Voices` folders, plus `Support/`
  (`TempDir` with an apostrophe in its path, `FakeSecretProtector`,
  `InMemorySecretStore`, `FakeHttpMessageHandler`, `FakeSpeechClient`).
- `tests/Omskep.App.Tests` (7 tests, **new project**, `net10.0-windows`):
  `DpapiSecretProtectorTests` against the real DPAPI.

## 3. Files changed

| File | Change |
| --- | --- |
| `src/Omskep.App/Program.vb` | Starts `MainForm` through `AppSession.Create`; adds the unhandled-exception safety net (shows only the error type) |
| `ATTRIBUTIONS.md` | Phase numbers corrected; DPAPI recorded as a .NET platform component; MSTest license wording |
| `README.md` | Phase 1 status, how the Azure key is stored and its scope, first-run behaviour |

Removed: `Form1.vb`, `Form1.Designer.vb`.

## 4. Targets reached

All seven items in the phase 1 starter prompt, plus the verification checklist.

1. `ISecretStore` and an in-memory fake: done (`ISecretProtector` added, see section 5).
2. DPAPI implementation, atomic write: done.
3. Settings file schema: done (version 1, unknown fields preserved).
4. REST client behind an interface with a fake handler: done.
5. Settings UI (masked key, region list, Test connection, Replace/Remove, never re-displaying the key): done.
6. Lockout state machine exactly as specified, plus extra states: done.
7. Voice list fetch and cache, refresh action, no built-in starter list: done.

### Settings schema (for later phases)

```json
{
  "schemaVersion": 1,
  "azure": { "region": "southafricanorth", "keyBlob": "<base64 of the DPAPI blob>" },
  "defaultVoices": { "af-ZA": "af-ZA-WillemNeural" },
  "speakingRatePercent": { "he-IL": -10 }
}
```

Add new settings as new properties with safe defaults. Unknown fields round-trip
untouched. Bump `schemaVersion` only for a change an old reader cannot ignore.
Files live in `%LOCALAPPDATA%\Omskep`: `settings.json`, `voices-cache.json`.

## 5. Deviations from plan

1. **Secret store split.** The plan had a DPAPI-only `ISecretStore` in the App.
   Instead `ISecretProtector` (Protect/Unprotect) is implemented by DPAPI in the
   App, and `FileSecretStore` lives in Core. Reason: the atomic write, damaged
   file and corrupt-blob handling are then testable on any platform, and one
   class owns the file.
2. **No ProtectedData package.** The phase 0 handoff said DPAPI needs the
   `System.Security.Cryptography.ProtectedData` package. On .NET 10 it ships
   in the framework and NuGet refuses the reference (NU1510).
3. **Voice cache in its own file** (`voices-cache.json`), not inside
   `settings.json`, so a bad cache can never endanger the key.
4. **Cache invalidation by region stamp**, not deletion. A cache for another
   region simply counts as missing and is overwritten by the next success.
5. **New `AzureOutcome` values.** `Transient` (408, 429, 5xx, timeout, cut-off
   download) and `Failed` (any other status, malformed body) alongside Ok,
   Rejected (401) and Offline. HTTP 429 is deliberately **not** treated as
   exhausted quota: Microsoft's TTS FAQ says it is usually regional or voice
   capacity.
6. **Sticky key rejection.** `ConnectionMonitor` keeps a 401 until a success or
   a key/region change, so a later offline blip cannot silently re-enable export.
7. **Extra lockout state `SettingsUnreadable`** (settings file locked or in use
   after retries): locked with a "try again" message, never treated as "no key".
8. **Save policy: a typed key or changed region is saved only when the test
   succeeds with voices.** Offline, busy and unexpected results save nothing,
   so a working key is never replaced by an unverified one (developer's
   decision). A candidate's failure is never reported to the connection monitor.
9. **Bounded retries** (5 tries, 25 to 400 ms) on the file swap and file read,
   because Windows antivirus and indexers briefly lock freshly written files.
   Found when a test failed once on the developer's machine.
10. **`SettingsStore.Update`** (atomic read-modify-write) added so two writers
    cannot lose each other's changes.
11. **Strict region validation and no redirects.** The region becomes part of
    the host name the key is sent to, so only lowercase letters and digits are
    accepted, and the shared `HttpClient` follows no redirects.
12. **`Omskep.App.Tests` project added** for Windows-only tests.
13. **Logic moved out of the forms** into `AppSession` and pure state classes,
    because WinForms code cannot be compiled or unit tested outside Windows.

## 6. Known issues and deferred items

- **Two writes for key plus region.** When one Test saves both a new key and a
  new region, they are two separate atomic writes. If the second fails after
  the first succeeds, the key and region can mismatch; one retry fixes it.
- **A well-formed but non-existent region** (for example a typo that is still
  letters and digits) fails DNS, which looks the same as "offline". The offline
  message therefore mentions the region spelling.
- **No automatic retry when the connection returns.** "Try again" is manual.
- **DPAPI scope.** It protects against other Windows users and against copying
  the settings file to another machine. It does not protect against other
  software already running as the same user. State this in the README.
- **`VoiceInfo` keeps only** ShortName, Locale, DisplayName, Gender, VoiceType.
  Phase 4 may need more (styles, sample rate); extend with a schema bump.
- **Default voices are not validated against the catalog.** `defaultVoices`
  may name a voice Microsoft later removes. Phase 4 must check with
  `VoiceCatalog.Contains`.
- **DPI mode** is still `SystemAware`, as scaffolded. `PerMonitorV2` and
  DirectWrite over Remote Desktop remain open items (phase 6).
- **No logging.** The safety net shows only the error type. Consider a local
  log file (never containing the key) in phase 6.
- **Intentional placeholders** (documented in code, remove in the phase shown):
  `MainForm` Open and Save handlers and the centred "editor arrives" label
  (phase 2), Export handler (phase 3).
- **Not covered by automated tests:** the two WinForms windows themselves
  (verified manually, below) and any call to the real Azure service.

## 7. Verification outcome

### Starter prompt checklist

| Check | Result |
| --- | --- |
| 1. Compiles with 0 warnings and errors | Yes |
| 2. Launches; with no settings, everything but Settings (and Exit) disabled; banner shown | Yes |
| 3. Valid key and region unlock the app and populate the voice cache ("Connected. 556 voices in 153 locales.") | Yes |
| 4. Wrong key gives the combined 401 message; offline gives the distinct message; neither saves anything | Yes |
| 5. Restart with a saved key stays unlocked with no request | Yes |
| 6. Hand-corrupted key blob: banner, no crash | Yes |
| 7. Unit tests pass | Yes: 229 of 229 (222 Core, 7 App) |
| 8. Placeholder audit | Yes: only the documented placeholders remain; spike folders and `%LOCALAPPDATA%\OmskepSpike` are gone; working tree clean |
| Settings window opens in the designer; survives a screen rotation (window is fixed size) | Yes |

### Must-never checks (phase 1 items)

| Must never | How it is covered |
| --- | --- |
| Store the key in plaintext where it could be exposed (source control, logs, UI after entry) | Tests: the saved file never contains the plaintext or base64 key; `Detail` and every user message never contain it; buffers wiped after use. UI: key box cleared on save, never re-filled. Settings live in `%LOCALAPPDATA%`, outside the repository. Manual: `settings.json` contains only the protected blob. |
| Automatically delete the key after a rejected request | Tests: key kept after a rejected startup, rejected refresh and rejected candidate. Manual: wrong replacement key left the saved key in place. |
| Crash on a corrupt settings file or key blob | Tests for damaged JSON, bad base64, undecryptable blob, locked file. Manual: hand-corrupted blob showed the banner. |

### Edge cases spot-checked

- Invalid or mistyped key (combined 401 message): manual, plus tests.
- Wrong region with a saved key: manual. The locked banner appeared, the saved key was kept, and correcting the region in Settings unlocked the app again.
- HTTP 429, 5xx, no network: client and session tests (fake handler); offline checked by hand.
- Several users with their own accounts, key as pure runtime configuration: no key or account is hard-coded anywhere; the blob is per Windows user.
- Windows profile path containing an apostrophe: all file tests run under one.

### Final checks (all done)

- [x] `%LOCALAPPDATA%\Omskep\settings.json` holds only a `keyBlob` field, no readable key.
- [x] Wrong-region check (region edited to another valid one, cache deleted): locked banner, key kept, recovered by correcting the region.
- [x] Placeholder audit: `git grep -n -i placeholder -- src` shows only the `lblPlaceholder` label and the two commented placeholder blocks in `MainForm.vb` (Open and Save for phase 2, Export for phase 3).
- [x] `spike\` and `%LOCALAPPDATA%\OmskepSpike` do not exist; `git status` is clean.
- [x] Test tooling recorded in `ATTRIBUTIONS.md`: MSTest 4.0.2 (MIT, microsoft/testfx).
- [ ] `git tag phase-1` placed on the final docs commit (the first tag, made before the docs commit, is local and unpushed on `dev`, so it is moved with `git tag -f phase-1`).

## 8. Suggested coordinator update

### Overall phase plan

Unchanged. Phase 1 is complete as scoped.

### Requirements document (`omskep-requirements.md`)

1. Edge case "Azure returns HTTP 429 (quota exhausted)": reword to "HTTP 429
   (usually capacity, not quota), a 5xx error, or no network". 429 is treated
   as transient and never as a bad key.
2. Open items still use the old phase numbers. Corrected: extraction strategy
   and second PDF are phase 2; offline/429/5xx handling is built and tested
   in phase 1 (client) and finishes in phase 3 (export); ID3v2.3 and
   DirectWrite are phase 6.
3. "API key and settings storage": record the save policy (typed key or region
   saved only after a successful test), the extra lockout states
   (`SettingsUnreadable`, transient failures), the separate `voices-cache.json`,
   and the region-stamped cache.
4. Key dependencies: no new libraries. DPAPI is part of .NET 10.
5. Reliability tiers: the Settings window is Important; `AppSession`, the
   state machine, `AtomicFile`, the stores and the client stay Critical.

### Phase 2 (editor) needs to know

- Read lock state and voices from `AppSession`: `Access.CanEdit`,
  `Catalog.VoicesFor(locale)`, `Catalog.Contains(shortName)`, `Region`.
- Write settings only through `SettingsStore.Update(Sub(s) ...)`, never
  load-then-save. Default voices are `defaultVoices` (locale to ShortName).
- `AppSession.StateChanged` can fire on any thread: marshal to the UI thread.
- Replace the `MainForm` placeholders and the "editor arrives" label; extend
  `MainFormState` for document state rather than adding logic to the form.
- WinForms gotcha: never name a variable after a `Control` property (`Region`
  broke the build in step 7).
- Windows-only tests go in `Omskep.App.Tests`.

### Phase 3 (export) needs to know

- Add `SynthesizeAsync` to `ISpeechClient`/`AzureSpeechClient` using the
  existing private `SendAsync`; its classification (401 Rejected, 408/429/5xx
  Transient, connection failure Offline, anything else Failed) is already
  tested. The 60 s timeout is for the voice list; synthesis needs its own.
- Report every Azure result with `ConnectionMonitor.Report(outcome)`, as
  `VoiceService.RefreshAsync` does. A mid-export 401 then blocks further
  exports without touching the stored key.
- Check `AppSession.Access.CanExport` before each export; get the key from
  `ISecretStore.Load()` per call and do not keep it in memory.
- Use the shared `HttpClient` from `AzureHttpClientFactory` (redirects off).
  `Retry-After` is not captured yet; add it if the resume logic needs it.
- For the "no half-written file under the final name" rule, reuse
  `AtomicFile`'s temp-then-swap approach (it may need a file-to-file variant
  for large audio).

### Later phases

- Phase 4: validate `defaultVoices` against the catalog; `VoiceInfo` may need
  more fields.
- Phase 6: README line on DPAPI scope; consider a local log file; revisit DPI
  mode; re-read Azure's output terms as already planned.

### `workflow.md` (VB.NET / WinForms section, "to be expanded")

Patterns that proved themselves in this phase:

- Put every decision in Core behind pure functions or small classes
  (`AppSession`, `SettingsFormState`) and keep forms thin.
- Test file code under a temp folder with an apostrophe in its name.
- Windows antivirus briefly locks freshly written files: retry the swap and
  the read a few times instead of failing.
- MSTest 4 analyzers are errors under warnings-as-errors: use `Assert.HasCount`,
  `IsEmpty`, `Contains`, `IsGreaterThan`, `ThrowsExactly`.
- VB cannot use `Span`; single-line lambdas cannot contain `Dim` or `Throw`;
  avoid identifiers that collide with VB or control members (`Input`, `Text`,
  `Region`).

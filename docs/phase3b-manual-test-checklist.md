# Phase 3b manual test checklist

Everything here is hand-testing through the real app -- the automated suite already
covers the logic in isolation; this is about confirming it's actually wired together
correctly and sounds/looks right. Work through roughly in order; later sections build
on documents created in earlier ones.

## 1. The omskep-meta block itself

- [ ] **File > New**: the document starts with the 8-line empty block (`Title:` through
      `Copyright:`, blank), then `<speak>`/`<voice>`. Caret lands on the first body line
      (the line right after `<voice ...>`).
- [ ] Status bar says **Well-formed** and **up to 0 characters** on this untouched new
      document -- confirms the block isn't tripping the checker and isn't being billed.
- [ ] Type a sentence in the body. The character count now increases by roughly the
      sentence's own length -- not inflated by anything from the meta block above it.
- [ ] Hand-edit a field, e.g. `Artist: Test Person`. Still **Well-formed**, count unchanged.
- [ ] Deliberately break the block (delete the closing `?>`). Confirm the live checker
      now flags an error, same as any other malformed markup, and **File > Export** greys out.
- [ ] Undo back to well-formed. Export re-enables (assuming the rest of the gate passes --
      see section 3).
- [ ] **Edit > Paste** (Ctrl+V) into a *blank* new document with some plain text on the
      clipboard: the result is a complete new document, still starting with the empty
      8-line block, not just the pasted text dropped into the existing one.

## 2. PDF import

Use a couple of real study PDFs you have on hand, ideally:
- one whose filename follows the `D_name (language).pdf` pattern,
- one that doesn't (to check the fallback),
- and, if you have one, a PDF where the Title/Author fields in its document
  properties are actually populated (most won't be, per your own notes -- worth
  trying one that is, if any exist, since that path is otherwise only unit-tested).

- [ ] **Numbered filename, no PDF Title set**: imported document's `Title:` is the
      `name (language)` portion of the filename, `Track:` is the leading number,
      `Album:`/`Copyright:` stay blank.
- [ ] **Filename doesn't match the pattern**: `Title:` is the whole filename (no
      extension), `Track:` stays blank.
- [ ] **A PDF with its own Title/Author set** (if you have one): `Title:` comes from
      the PDF's own metadata rather than the filename; `Artist:` is populated from
      the PDF's Author.
- [ ] **Year**: matches the PDF's creation year (check the PDF's own "Document
      Properties" dialog in a PDF reader to confirm what year it should show).
- [ ] Import twice in a row (same file): both imports produce the same meta block
      content -- nothing accumulates or duplicates.

## 3. The export dialog -- confirmation screen

Open a document with real body text (anything from section 1 or 2 works).

- [ ] **File > Export** opens the dialog showing the **up to N characters** count,
      matching (or very close to) what the status bar already showed.
- [ ] All eight fields show the *resolved* values -- whatever you typed into the
      meta block (or left blank) for Title/Artist/Album/Track/Year/Copyright, and
      the **computed** Comment/Genre (since you haven't hand-typed either yet).
- [ ] Try selecting text in any of the plain fields (Title, Artist, etc.) with the
      mouse -- it should select and be copyable like normal text, not look greyed out.
- [ ] Click the Comment **Copy** button, then paste somewhere (Notepad, the editor
      itself) -- confirm it's exactly the Comment field's full text.
- [ ] The computed Comment text itself: check it names the correct voice(s), the
      correct speaking rate, the source filename, and the synthetic-speech disclosure.
- [ ] Click **Cancel** on this screen -- dialog closes, nothing written, document
      unchanged.
- [ ] Reopen Export, this time click **Export…** then **Cancel the Save As dialog**
      (don't pick a file) -- confirm you land back on the *confirm* screen, not an
      error, and can click Export again.

## 4. Comment/Genre: typed vs. computed

- [ ] Hand-type `Comment: My own note` into the document's meta block. Reopen Export
      -- Comment field now shows exactly `My own note`, not the computed text.
- [ ] Hand-type `Genre: Sermon`. Reopen Export -- Genre shows `Sermon`, not `Audiobook`.
- [ ] Remove both lines again (or blank them). Reopen Export -- both fall back to
      computed values again.

## 5. Naming

- [ ] First export of a never-exported document: Save As opens pre-filled with
      `<document name> Alpha.mp3`.
- [ ] Complete that export, then export the *same* document again: Save As this
      time defaults to `<document name> Alpha (2).mp3`.
- [ ] In the Save As dialog, type something completely different (e.g. `RC1.mp3`)
      instead of accepting the default -- confirm it saves under that name with no
      complaint, since the app has no opinion about what you actually name it.
- [ ] Export an **unsaved** ("Untitled") document -- default name is `Untitled Alpha.mp3`.

## 6. A real export, start to finish

Pick a document long enough to produce **at least 2-3 chunks** (a full-length study,
not a one-sentence test) so the progress bar and multi-chunk paths actually exercise.

- [ ] Progress bar and status text update sensibly as chunks send (chunk X of Y).
- [ ] On completion: result screen shows success and the output path; **Close**
      closes the dialog.
- [ ] Open the resulting file in **foobar2000** and **Windows Explorer's own
      Properties > Details tab**. Confirm every field: Title, Artist, Album, Track
      (as a number, not text), Year, Genre, Comment, and -- Explorer doesn't show a
      dedicated Copyright field, so check that one in foobar2000 specifically, or any
      tag editor that exposes the raw ID3 frame.
- [ ] **Listen at every chunk seam** -- same check as phase 3a, now through the real
      export path rather than the throwaway harness.
- [ ] Try a document whose Title/Artist/Comment contain **non-ASCII characters**
      (Afrikaans diacritics, Hebrew text, an em dash) -- confirm they render correctly
      in both tools, not as mojibake or `?` placeholders.

## 7. Cancellation

- [ ] Start an export on a multi-chunk document, click **Cancel** while a chunk is
      actively sending (watch the status text). Confirm: no output file is left
      behind, and the dialog shows a sensible "cancelled" result rather than crashing
      or hanging.
- [ ] Note for your own awareness, not something to "fix" by retrying harder: per the
      documented gap from `ExportEngineTests`, a cancel that lands *between* chunks
      comes back as a clean result; one that lands *while a chunk is in flight* is
      caught separately in the dialog's own `Catch OperationCanceledException`. Both
      paths should still end with no stray file and a readable message -- if either
      one instead throws an unhandled exception up to the IDE/crashes the app, that's
      a real bug to report back, not an expected rough edge.

## 8. Failure paths (deliberately provoked)

These need you to break something on purpose -- genuinely hard to hit by accident,
which is exactly why they're worth checking deliberately rather than trusting the
unit tests alone for the *wiring*, even though the logic itself is already covered.

- [ ] **Bad key/region (401)**: in Settings, temporarily enter an invalid key (or
      point the region somewhere your key doesn't cover) so `Test connection` fails,
      or if you can still force a stored-but-now-invalid key through, export and
      confirm you get a clear "key or region rejected" message, and that **Export
      stays blocked afterward** until you fix it in Settings again (not just that one
      attempt failing) -- this is the sticky-rejection behavior from `ExportEngine`.
- [ ] **Offline**: disconnect from the network (airplane mode / disable Wi-Fi)
      mid-export, or before starting one. Confirm a clear "could not reach Azure"-
      style message, not a raw exception dialog.
- [ ] **Write failure**: start an export, and *before* confirming the Save As
      dialog, open the target file in another program that locks it (or pick a
      read-only folder) -- actually, simplest: let an export **succeed** once, then
      immediately open that exact MP3 in another program (so it's locked), and try
      exporting *to that same path* again. Confirm: a message along the lines of
      "the audio exported fine, but saving failed," **not** a generic chunk-failure
      message -- confirms `ExportStatus.FailedWriteOnly` is actually reaching the
      dialog correctly, not just existing in `ExportEngine`.
- [ ] Immediately retry that same export (close the locking program first) --
      confirm it completes quickly, consistent with **not** re-synthesizing audio
      that already succeeded (you won't see this directly, but a near-instant
      second attempt vs. a full multi-chunk wait is a reasonable proxy).

## 9. Quick regression of phases 1-3a

Not new code, but cheap to confirm nothing in this phase's document-model changes
(the 8-line scaffold shift, the new BodyStartLine, etc.) broke anything older:

- [ ] Settings still locks everything but Settings itself without a working key.
- [ ] Editor, Find/Replace, Hebrew reversal, and recovery-on-crash all still behave
      as before.
- [ ] Open an **old `.ssml` file saved before this phase existed** (no meta block at
      all) -- confirms the parser's "absent = null" behavior holds for real legacy
      files, not just the test fixtures, and that the document still opens, edits,
      and exports normally despite having no `omskep-meta` block whatsoever.

---

Anything that doesn't match what's described here is worth bringing back with
specifics (what you did, what you expected, what you saw) -- several of the subtler
paths above (write-failure wording, the resume-without-resynthesis case, the two
cancellation shapes) have never actually run end-to-end until now.

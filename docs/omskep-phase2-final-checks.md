Omskep phase 2: final in-app checks
===================================

Work through this after the latest code is in, all unit tests pass and the
retests of the last round are done. Tick each box; write anything unexpected
next to it. About 45 to 60 minutes.

Legend: **Expect** is what must happen. Items marked **(M)** are "must never"
items from the requirements.

0. Preparation
--------------

Files to have ready (all local, none committed):

-   [x] The 45-page Afrikaans PDF (largest study) and the English PDF with
    tables

-   [x] A scanned or image-only PDF (any PDF made from a photo or screenshot)

-   [x] A password-protected PDF (for example Word, Save As PDF, with a
    password; or any PDF tool)

-   [x] A damaged file: copy a text file and rename it `broken.pdf`

-   [x] A Windows-1252 text file with `é` or `ë` (Notepad, Save As, Encoding:
    ANSI)

-   [x] A UTF-16 text file (Notepad, Save As, Encoding: UTF-16 LE)

-   [x] A few lines of Word text with curly quotes, a literal `&`, and `‘n`
    before a word

-   [ ] A second copy of `harness` output for the Afrikaans PDF
    (`*.contentorder.extracted.txt`), for the comparison in 2.2

-   [x] Your Azure key is saved and works (or note which checks need it)

Know where things live: `%LOCALAPPDATA%\Omskep` (settings.json, voices cache)
and `%LOCALAPPDATA%\Omskep\recovery`.

1. Regression: phase 1 is unchanged
-----------------------------------

-   [x] **1.1** With no key saved, the app launches locked: banner "Enter your
    Azure key...", Open Settings works, New/Open/Paste are disabled, the editor
    is read-only

-   [x] **1.2** Enter a working key: banner disappears, editor unlocks, status
    bar shows "Ready. N voices available."

-   [x] **1.3 (M)** Search `settings.json` and the whole `recovery` folder for
    any part of your key: **Expect** not found

-   [x] **1.4 (M)** Enter a wrong key in Settings and Test: **Expect** it is
    refused and your saved working key is not replaced or deleted

-   [x] **1.5 (M)** Key is never shown again after entry (Settings shows it
    masked or not at all)

2. Happy path (requirements steps 1 and 2, plus save and reopen)
----------------------------------------------------------------

-   [x] **2.1** File, Open, choose the Afrikaans PDF. **Expect** a "Imported 45
    pages. Cleaned up: removed 4 decorative symbols, corrected 22 quote marks.
    For review: 45 wide gaps and 22 line-end hyphens." dialog, the document in
    the editor with a line-number gutter, window title `Untitled*`, status bar
    `up to ~80,000 characters`, yellow gap marks

-   [x] **2.2 (M)** Nothing silently stripped: Save As `check.ssml`, then
    compare it with the harness `contentorder.extracted.txt` in a diff tool.
    **Expect** only these differences: the four ornament characters, 22 `‘n`
    changed to `'n`, `&` written as `&amp;`, trailing spaces trimmed,
    `<speak>`/`<voice>` wrapping, one blank line between pages. All 44 header
    lines and all page-number lines are still there

-   [x] **2.3** Delete a header line and a page number by hand. **Expect** the
    character count drops

-   [x] **2.4** Save, close the app normally, reopen, File, Open the `.ssml`.
    **Expect** text identical (compare with a diff tool), title without a star,
    line numbers the same

-   [x] **2.5** File, Open the English PDF with tables. **Expect** a sensible
    reading order for the table rows (compare with the harness output)

-   [x] **2.6** Paste: copy the Word text, File, New, Ctrl+V. **Expect** a
    complete document, `&amp;`, curly quotes kept, `'n` fixed, note "Imported
    the pasted text."

-   [x] **2.7** Edit menu, Paste as plain text (Ctrl+Shift+V) with the same text
    in a document that has content. **Expect** exactly what was copied, a red
    squiggle at the bare `&`, and the status bar naming the line

-   [x] **2.8** New (Ctrl+N). **Expect** a scaffold with `<voice name="...">`
    from your saved default voice, caret on line 3

-   [x] **2.9** Open a `.txt` file of plain text. **Expect** it is imported as a
    new unsaved document, not opened raw

3. Must-never checks for this phase
-----------------------------------

### 3.1 (M) Never lose unsaved edits

-   [x] **3.1a** Import the PDF, wait 6 seconds, kill Omskep in Task Manager
    (End task). Restart. **Expect** the recovery prompt with the document; Yes
    restores it, title shows `*`

-   [x] **3.1b** Type 10 words, wait 6 seconds, kill it, restart, restore.
    **Expect** all 10 words

-   [x] **3.1c** Type a word and kill within 2 seconds. **Expect** the word may
    be missing (this is the documented window of up to 5 seconds); everything
    older is there. Note the result

-   [x] **3.1d** Restore, then Cancel on the next prompt for another leftover.
    **Expect** it is offered again at the next launch; choosing No deletes it
    for good

-   [x] **3.1e** Open an existing `.ssml`, edit it, kill the app, restart,
    restore. **Expect** the restored document knows its original file; Save
    writes to that file

-   [x] **3.1f** Close the window with unsaved changes. **Expect** Save / Don't
    save / Cancel; Cancel keeps you in the app; Save with a failing path keeps
    the window open

-   [-] **3.1g** Windows sign-out or restart with unsaved edits (optional, only
    if safe to do). **Expect** no blocking prompt and a recovery offer next time

-   [x] **3.1h** Two instances at once, each with a different unsaved document.
    Kill one. Restart. **Expect** only the killed one's work is offered, and the
    running instance is not disturbed

-   [x] **3.1i** A normal save, then close. **Expect** no recovery prompt next
    time, and the `recovery` folder is empty

### 3.2 (M) Never silently strip text; boilerplate removal is user-driven

-   [x] **3.2a** Covered by 2.2. Also confirm the import report listed every
    cleanup it made

-   [x] **3.2b** Import a PDF where only some pages are empty (or reduce an
    image-only page into a text PDF). **Expect** the empty page numbers are
    named in the dialog

### 3.3 (M) Never produce or accept malformed SSML silently; never corrupt on save

-   [x] **3.3a** Type each of these in the body, one at a time, and wait half a
    second. **Expect** a red squiggle and a `Line N: ...` message for every one,
    never "Well-formed": `<break time=` / `<prosody>` with no closing tag /
    `</voice>` extra / `a & b` / `a < b` / `<b>unclosed` / delete the final
    `</speak>`

-   [x] **3.3b** With an error showing, File, Save. **Expect** saving works.
    Close and reopen the file. **Expect** the file contains exactly what was in
    the editor, error included (saving never repairs or blocks)

-   [x] **3.3c** With an error showing, check File, Export. **Expect** disabled,
    with a tooltip such as "Fix the markup error on line N first."

-   [x] **3.3d** Remove the error. **Expect** the squiggle goes, status says
    Well-formed, Export enables if a key is saved

-   [x] **3.3e** Sanitizing paste of text containing `<`, `>`, `&`, `]]>`,
    `<!--`. **Expect** well-formed result every time

### 3.4 (M) Never auto-correct markup

-   [x] **3.4** After 3.3a, Edit, Undo until clean. **Expect** the text you
    typed is exactly what you typed; nothing was fixed or changed by the checker

### 3.5 (M) Live checker never says valid when malformed, and does not lag on the largest document

-   [x] **3.5a** On the 80,000-character document, type continuously for 20
    seconds and hold Backspace. **Expect** no stutter; the status text updates
    about 0.4 seconds after you stop

-   [x] **3.5b** Type half a tag slowly (`<`, `<b`, `<br`, `<break`). **Expect**
    no flashing red between keystrokes, one result after you pause

-   [x] **3.5c** While an error is showing, is "Well-formed" ever shown at the
    same time as a squiggle? **Expect** never

-   [x] **3.5d** Press F8. **Expect** the caret jumps to the error. For an
    unclosed tag, Shift+F8 jumps to the probable cause (the element that was
    never closed)

-   [x] **3.5e** Scroll, undo 50 steps, redo 50 steps, resize the window.
    **Expect** smooth

### 3.6 Later phases, not tested now

Recorded so nothing is forgotten: export count confirmation, overwrite
protection and chunking (phase 3); script mismatch warning and preview scoping
(phases 4 and 5); bulk replacement scoping (phase 5).

4. Real-world edge cases
------------------------

-   [x] **4.1** Scanned PDF. **Expect** the message about a scanned or
    image-only document, no document change, nothing marked dirty

-   [x] **4.2** Password-protected PDF. **Expect** the password message, current
    document untouched

-   [x] **4.3** `broken.pdf`. **Expect** "could not be read as a PDF", no crash

-   [ ] **4.4** Windows-1252 text file via Open. **Expect** the "not UTF-8"
    message that says how to fix it

-   [x] **4.5** UTF-16 file with a BOM. **Expect** it opens (as text, imported)

-   [ ] **4.6** A file open in another program with an exclusive lock.
    **Expect** a clear message, no crash

-   [x] **4.7** Save As onto a read-only location (for example `C:\Windows`).
    **Expect** a clear message, "Your text is still in the editor", still dirty,
    still recoverable

-   [x] **4.8** Save As onto an existing file. **Expect** the overwrite
    question; answering No changes nothing

-   [x] **4.9** Save into a folder with an apostrophe and Unicode in its name,
    for example `D'Brien Gelees ë`. **Expect** works, and the file reopens

-   [x] **4.10** Line numbers stay reliable: note the text of line 200, delete
    five lines above it. **Expect** that text is now on line 195; the yellow
    marks stayed with their text

-   [ ] **4.11** Hebrew in the Afrikaans study reads in the right order; moving
    the caret through it is a little unusual (accepted)

-   [x] **4.12** Word wrap: off gives a horizontal scrollbar on a narrow window,
    on wraps without moving line numbers

-   [x] **4.13** Small document (type three words) and the 80,000-character one.
    **Expect** the character count is visible and sensible at both ends

5. Editing and window behaviour
-------------------------------

-   [x] **5.1** Title star: type a letter, then undo it. **Expect** the star
    disappears when the text matches the saved file again (for a file that
    exists)

-   [x] **5.2** Dirty prompts: with unsaved changes, use New, Open and Exit in
    turn. **Expect** Save / Don't save / Cancel each time

-   [x] **5.3** One Undo reverses one Paste, and one Undo restores the old text
    after a whole-document paste

-   [x] **5.4** Right-click the editor. **Expect** our menu (Undo, Redo, Cut,
    Copy, Paste, Paste as plain text, Select all), not Scintilla's. Right-click
    Paste behaves like Ctrl+V

-   [x] **5.5** Lock in mid-session: with unsaved changes, Tools, Settings,
    remove the key. **Expect** the editor turns read-only, **Save and Save As
    stay enabled**, New/Open disabled. Re-enter the key and editing returns

-   [x] **5.6** Word wrap setting is remembered after a normal close and reopen

-   [x] **5.7** Narrow the window while a long error message is showing.
    **Expect** the status text is shortened with "...", full text appears as a
    tooltip, nothing overlaps

-   [x] **5.8** Current line highlight is pale and text stays readable

-   [x] **5.9** Windows display scaling at 125% or 150% if you can try it
    (Settings, System, Display). **Expect** menus, gutter and status bar look
    right

-   [x] **5.10** During a PDF import the wait cursor shows and menus are
    disabled; trying to close says to wait

6. Known gaps to record in the handoff (check, do not expect them to pass)
--------------------------------------------------------------------------

-   [ ] **6.1** Drag text from another program into the editor. **Expect** it is
    inserted raw, like Paste as plain text (not sanitized). Record the result

-   [ ] **6.2** There is no Find or Replace yet. Note how painful it is to
    remove the repeated headers by hand (this decides whether it joins this
    phase)

-   [ ] **6.3** The "DirectWrite off" setting is not built yet (the control
    supports it; there is no menu or saved setting). Decide: this phase or phase
    6

-   [ ] **6.4** The "Remove repeated lines" helper we discussed is not built

-   [ ] **6.5** Autosave runs every 5 seconds, so a kill can lose up to 5
    seconds

-   [ ] **6.6** Only the newest leftover recovery is offered with an in-session
    restore; older ones are kept

Sign-off
--------

-   [ ] All of sections 1 to 5 pass, or each failure is written down with what
    you saw

-   [ ] Section 6 results recorded for the handoff

-   [ ] Changes committed, release tagged (`phase2`) once everything is accepted

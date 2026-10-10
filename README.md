# Omskep

Omskep ("to transform" in Afrikaans) converts weekly Bible study PDFs, mixed
Afrikaans and English, into narrated MP3 files using Azure AI Speech neural
voices. It is built for personal listening and for sharing a cleaned-up,
proof-listened audio version with a study group.

## Status

Phases 0 (technical decisions, spikes, scaffold), 1 (Settings and Azure
connection) and 2 (the SSML editor) are complete. You can now import a study
PDF or paste text, clean it up in a line-numbered editor that checks your markup
as you type, and save it as an `.ssml` file. Omskep is **not yet a complete
converter**: exporting audio, choosing a voice per language, the pronunciation
tools and the SSML reference panel are built in later phases. The File, Export
item is still a placeholder.

## What it will do

1. Open a study PDF (or paste text from Word) and turn it into an editable
   SSML document. SSML (Speech Synthesis Markup Language) is the saved file
   format, shown as raw text in a line-numbered editor. *(Done.)*
2. Let you remove page headers, footers and extraction artefacts, tag
   language changes, and choose a voice per language. *(Cleaning is done; tagging
   and voice choice come in phase 4.)*
3. Show the billable character count before anything is sent to Azure. *(The
   running count is done; the final count before export comes in phase 3.)*
4. Export the document as one MP3 with ID3 tags, named so that first-pass
   (`alphaN`) and Release Candidate (`RCN`) files can never overwrite each
   other. *(Phase 3.)*
5. Let you listen, note corrections by line number, fix pronunciations
   (respelling, `<sub alias>`, or `<phoneme>`), and export again. *(Phases 4 and 5.)*

## Requirements

- Windows 10 or later
- .NET 10 runtime (framework-dependent deployment; not bundled)
- Your own Azure AI Speech resource (key and region). Omskep never ships or
  assumes a key; it is entered in Settings and stored encrypted for your
  Windows user account.

## Settings and your Azure key

Open **Tools > Settings**. Until a working key is saved, everything except
Settings is disabled.

1. Choose your Azure region. South Africa North (`southafricanorth`) is the
   default; any other region id, such as `westeurope`, can be typed.
2. Paste your key into the masked box and press **Test connection**.
3. Omskep saves the key and region only if the test succeeds. Otherwise
   nothing changes, so a working key is never replaced by one that does not
   work.

After a successful test Omskep downloads the list of voices for your region
and keeps it on disk, so later launches do not need the network. **Refresh
voices** downloads it again, for example when Microsoft adds a voice. The key
is never shown again after entry; use **Replace...** or **Remove** to change
it.

If Azure later rejects the key or region, you can keep editing but export is
blocked until you fix it in Settings. The saved key is never deleted
automatically. A region name that looks valid but does not exist fails like a
lost connection, so check its spelling if you see the "could not reach Azure"
message.

If you remove the key while a document has unsaved changes, the editor becomes
read-only but **Save and Save As stay available**, so nothing is ever trapped.

### Where your key is kept

Settings are stored in `%LOCALAPPDATA%\Omskep` (`settings.json` and
`voices-cache.json`). The key is encrypted with Windows' per-user protection
(DPAPI), so another Windows user, or a copy of the file on another computer,
cannot read it. It does **not** protect against other software that is already
running as you. Do not share the settings file or put it in source control.

## Using the editor

### Starting a document

- **File > New** (Ctrl+N): a blank document with the `<speak>` and `<voice>`
  scaffolding, using your default voice. The caret waits on line 3.
- **File > Open** (Ctrl+O) opens:
  - `.ssml` and `.xml` files exactly as they are;
  - `.pdf` study files, whose text is extracted and imported as a new, unsaved
    document;
  - any other file as plain text to import (unless it already starts with
    `<speak`, in which case it is opened as SSML). Text files must be UTF-8 or
    UTF-16 with a byte-order mark; other encodings, such as Windows ANSI, are
    refused with instructions, never guessed.
- **Edit > Paste** (Ctrl+V) sanitizes whatever is on the clipboard. Into an empty
  document it builds a complete document; into a document with content it inserts
  the cleaned text at the caret.
- **Edit > Paste as plain text** (Ctrl+Shift+V) inserts exactly what was copied,
  with no cleaning. A raw `&` will then show up as a markup error, which is
  deliberate.
- Double-clicking a `.ssml` file, or choosing **Open with** and picking
  `Omskep.App.exe` (tick "Always" to make it the default), opens it in Omskep.

### What import cleans, and what it never does

Import removes decorative private-use symbols, control characters, and stray
surrogates; changes the wrong-direction quote before the Afrikaans article
(`‘n` becomes `'n`); normalises Hebrew presentation forms (Unicode NFC); trims
trailing spaces; and escapes `&`, `<` and `>`. A dialog tells you exactly what
it did.

It **never removes words**. Page headers, footers and page numbers stay in the
document for you to remove. Places that deserve a look are highlighted in
yellow instead: runs of three or more spaces (usually a header gap) and lines
that end in a hyphen after a letter (possibly a word split across lines). Turn
the highlighting off or on under **View > Highlight review marks**. Pages with no
readable text (a scanned page) are named in the dialog; a PDF with no text at all,
or a password-protected one, gets a clear message and changes nothing.

### Removing headers and other repeated text

**Edit > Find** (Ctrl+F) and **Replace** (Ctrl+H) open one small window that stays
open while you work. F3 and Shift+F3 find the next and previous match. Options
are match case, whole word and regular expression. Replace All is one undo step
and reports how many it replaced.

With "Regular expression" ticked, one pattern can remove a header and its page
number everywhere. For example, if every page starts with the lines
`Torah Navorsing Akademie` (some spaces) `Die Heelal: Toeval?` and a page number,
find

    ^Torah Navorsing Akademie\s+Die Heelal: Toeval\?\n\d+ ?\n

and replace it with nothing. In a replacement, `$1` is the first captured group,
`\n` a line break, `\t` a tab. A pattern that takes too long is stopped with a
message rather than freezing the program.

### Hebrew from PDFs

PDFs store Hebrew in the order it is drawn, so imported Hebrew arrives with its
letters reversed (it reads left to right) and would be spoken backwards.
**Edit > Reverse Hebrew text** puts it right: on the selection if there is one,
otherwise on the whole document (leaving runs that already look correct alone,
after asking). It is also in the right-click menu and is one undo step. In
pointed (vowelled) text a vowel mark can end up beside the wrong letter; the
marks are not spoken, so the order of the letters is what matters.

### Live checking

While you type, Omskep checks that the markup is well-formed XML about 0.4 seconds
after you pause. Problems get a red squiggle and a message in the status bar
(`Line N: ...`). For an unclosed tag it also marks, in orange, the element that is
probably still open, because the parser only notices at a later closing tag.
**F8** jumps to the error and **Shift+F8** to that probable cause. The checker
only reports; it never changes your text, and saving is never blocked.
**File > Export** stays disabled while the document has an error.

The status bar also shows `up to N characters`: an upper bound on the characters
Azure will bill for the document as written (everything except the `<speak>` and
`<voice>` tags, a line break counting as one). The final count, including anything
added at export, is shown before export in a later phase.

### Saving, autosave and recovery

**Save** (Ctrl+S) and **Save As** (Ctrl+Shift+S) write UTF-8 without a byte-order
mark, with LF line endings, through a write-then-replace step so that a failure
leaves the old file intact. A star in the title bar means unsaved changes, and it
disappears if you undo back to the saved text.

While you have unsaved changes Omskep keeps a recovery copy in
`%LOCALAPPDATA%\Omskep\recovery`, refreshed every five seconds, so a crash,
power cut or "End task" loses at most the last few seconds of typing. At the next
start it offers the unsaved work: **Yes** restores it, **No** discards it,
**Cancel** keeps it for next time. Recovery files are removed when you save, start
a new document or close the program normally, and are never deleted without
your say-so.

### View options and keys

**View** has Word wrap, Highlight review marks and Smooth text rendering
(DirectWrite; turn it off if text looks wrong on your machine). All three are
remembered.

| Keys | Action |
| --- | --- |
| Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S | New, Open, Save, Save As |
| Ctrl+V, Ctrl+Shift+V | Paste (sanitizing), Paste as plain text |
| Ctrl+F, Ctrl+H, F3, Shift+F3 | Find, Replace, Find next, Find previous |
| F8, Shift+F8 | Go to error, Go to probable cause |
| Ctrl+Z, Ctrl+Y | Undo, Redo |

## Azure account, costs and responsible use

Omskep talks to Azure AI Speech over its plain HTTPS (REST) interface. It does
not include or redistribute the Azure Speech SDK.

- Azure bills per character synthesized. Omskep shows an upper bound of the
  billable characters before each export. Check Azure's pricing page for
  current rates.
- Azure's free tier is intended for evaluation. **The author's intent is that
  audio you distribute to other people is generated with a paid Speech
  resource**, as a fair way to support the service. Omskep cannot tell which
  tier a key belongs to, so this is guidance, not a technical restriction. If
  you fork or reuse this code, please read Microsoft's terms for the output of
  text to speech and decide for yourself:
  - Azure legal hub (Product Terms): <https://azure.microsoft.com/en-us/support/legal/>
  - Microsoft AI Services Code of Conduct: <https://learn.microsoft.com/en-us/legal/ai-code-of-conduct>
  - Azure Speech text to speech code of conduct: <https://learn.microsoft.com/en-us/legal/cognitive-services/speech-service/text-to-speech/code-of-conduct>
- The narration is synthetic speech. Microsoft asks that listeners are told
  when a voice is AI-generated, so say so when you share the audio. Exported
  files will carry a note in their ID3 comment.
- Only the study documents you are allowed to convert should be converted.
  Check with the author or publisher before distributing audio made from
  their material.

## Known limitations

- Language tagging works at block level. A single sentence that mixes
  Afrikaans and English is read by one voice.
- There is no Latin voice available, so Latin text is not supported. Hebrew
  and Greek use their own voices (Greek is read by a modern Greek voice).
- Pronunciation of names and places often needs manual correction; this is
  what the correction tools are for.
- Hebrew extracted from a PDF arrives in reverse order and needs **Edit >
  Reverse Hebrew text** (see above). Moving the caret through right-to-left text
  is slightly unusual.
- The quote before the Afrikaans article is written as a straight `'n`. If it does
  not sound right in the audio, it is a single constant in `TextSanitizer`.
- Text dragged into the editor from another program is inserted as it is (like
  Paste as plain text), not sanitized.
- The recovery copy is refreshed every five seconds, so a crash can lose the last
  few seconds of typing.
- Scanned (image-only) PDFs cannot be read; there is no OCR.

## Building from source

- .NET 10 SDK
- Visual Studio 2026 (or later) with the ".NET desktop development" workload
- Open `Omskep.slnx`, or build from the command line: `dotnet build`
- NuGet restores the dependencies: `PdfPig` (Core) and `Scintilla5.NET` (App).
- Run the tests with `dotnet test` (it runs both test projects; the App tests
  need Windows)
- Use a source path without apostrophes or other unusual characters.

## Project structure

- `src/Omskep.Core`: logic with no UI or platform dependencies.
  - `Access`, `Secrets`, `Session`, `Settings`, `Speech`, `Storage`, `Voices`:
    settings and key storage, the Azure Speech REST client, the voice list and
    its cache, the lockout rules, and the form-state classes.
  - `Import`: text sanitizing, PDF text extraction (`PdfPig`), the importer, the
    paste decision, and the review-flag finder.
  - `Documents`: well-formedness checking, the billable-character counter, the
    document state model, file open and save, the recovery store, find and
    replace, and Hebrew order repair.
  - Chunking and export logic follow in phase 3.
- `src/Omskep.App`: WinForms application (`net10.0-windows`): the main window,
  Settings, the Find and Replace window, `Editor\SsmlEditor` (the wrapper around
  the Scintilla5.NET control), and the Windows DPAPI key protector
- `tests/Omskep.Core.Tests`: MSTest unit tests for `Omskep.Core`
- `tests/Omskep.App.Tests`: MSTest tests that need Windows
  (`net10.0-windows`), such as the DPAPI key protector
- `docs`: phase handoff documents and test checklists

## Third-party software

See `ATTRIBUTIONS.md`.

## License

BSD-3-Clause. See `LICENSE`.

# Omskep

Omskep ("to transform" in Afrikaans) converts weekly Bible study PDFs, mixed
Afrikaans and English, into narrated MP3 files using Azure AI Speech neural
voices. It is built for personal listening and for sharing a cleaned-up,
proof-listened audio version with a study group.

## Status

Phase 0 (technical decisions, spikes and project scaffold) is complete.
Omskep is **not yet functional**: the application is an empty shell while the
editor, import, export and voice features are built in later phases.

## What it will do

1. Open a study PDF (or paste text from Word) and turn it into an editable
   SSML document. SSML (Speech Synthesis Markup Language) is the saved file
   format, shown as raw text in a line-numbered editor.
2. Let you remove page headers, footers and extraction artefacts, tag
   language changes, and choose a voice per language.
3. Show the billable character count before anything is sent to Azure.
4. Export the document as one MP3 with ID3 tags, named so that first-pass
   (`alphaN`) and Release Candidate (`RCN`) files can never overwrite each
   other.
5. Let you listen, note corrections by line number, fix pronunciations
   (respelling, `<sub alias>`, or `<phoneme>`), and export again.

## Requirements

- Windows 10 or later
- .NET 10 runtime (framework-dependent deployment; not bundled)
- Your own Azure AI Speech resource (key and region). Omskep never ships or
  assumes a key; it is entered in Settings and stored encrypted for your
  Windows user account.

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
- The editor shows Hebrew in the correct order, but moving the caret through
  right-to-left text is slightly unusual.

## Building from source

- .NET 10 SDK
- Visual Studio 2026 (or later) with the ".NET desktop development" workload
- Open `Omskep.slnx`, or build from the command line: `dotnet build`
- Run the tests with `dotnet test`
- Use a source path without apostrophes or other unusual characters.

## Project structure

- `src/Omskep.Core`: SSML sanitization, chunking, billable-character
  counting, well-formedness checking and other logic with no UI or platform
  dependencies
- `src/Omskep.App`: WinForms application (`net10.0-windows`)
- `tests/Omskep.Core.Tests`: MSTest unit tests for `Omskep.Core`
- `spike/`: reserved for throwaway exploration code; never part of the
  shipping app

## Third-party software

See `ATTRIBUTIONS.md`.

## License

BSD-3-Clause. See `LICENSE`.

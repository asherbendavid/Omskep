# Attributions

Third-party software and services used by Omskep, with license and source.
This file is updated by every phase as dependencies are added or removed.

Licenses below were checked against the package or project license text during
phase 0 (October 2026), and the test tooling again in phase 1. Phase 2 put PdfPig
and Scintilla5.NET into use; their licenses were not re-read in phase 2, so
re-check them against the exact pinned versions before the first release (see the
list at the end). Re-check them whenever a version changes.

## Libraries used or planned

| Component | Purpose | Introduced in | In use since | License | Source |
| --- | --- | --- | --- | --- | --- |
| PdfPig (version pinned in `Omskep.Core.vbproj`) | PDF text extraction (`ContentOrderTextExtractor`) | Phase 2 | Phase 2 | Apache-2.0 (derived from Apache PDFBox) | <https://github.com/UglyToad/PdfPig>, NuGet package `PdfPig` |
| Scintilla5.NET 7.0.0 | WinForms editor control (`Omskep.App`, `Editor\SsmlEditor`) | Phase 2 | Phase 2 | MIT | <https://www.nuget.org/packages/Scintilla5.NET> |
| Scintilla and Lexilla (native engine bundled inside Scintilla5.NET) | Text editing engine and XML lexer | Phase 2 | Phase 2 | Scintilla license (short permissive notice), Copyright 1998-2021 Neil Hodgson | <https://www.scintilla.org/License.txt> |
| TagLibSharp2 0.6.0 | ID3v2 tags on exported MP3 files | Phase 3 | Not yet | MIT | <https://github.com/decriptor/TagLibSharp2>, NuGet package `TagLibSharp2` |

Development and test only (not distributed with the application):

| Component | Purpose | License |
| --- | --- | --- |
| MSTest 4.0.2 (microsoft/testfx; meta package that pulls in MSTest.TestFramework, MSTest.TestAdapter, Microsoft.NET.Test.Sdk and the code-coverage extensions) | Unit tests | MIT |

The MSTest license was read on NuGet's page for the latest version (4.4.1); the
pinned 4.0.2 is the same project (microsoft/testfx). None of these test packages
is distributed with the application.

A throwaway console project used once in phase 2 to compare PdfPig's two text
extraction methods (`page.Text` and `ContentOrderTextExtractor`) was never
committed and is not part of the product. It used PdfPig only.

## Platform components (part of .NET, not separate dependencies)

- **Windows DPAPI** (`System.Security.Cryptography.ProtectedData`) protects the
  saved Azure key. It ships inside .NET 10 for Windows, so no package is
  referenced (NuGet reports NU1510 if one is added). Governed by the .NET
  runtime's MIT license. If Omskep is ever published self-contained, include
  the .NET runtime's license notices with the release.
- **System.Text.Json**, **System.Text.RegularExpressions**, **System.Xml** and
  the Unicode normalization in `System.String` (NFC) ship inside .NET and are
  used for the recovery files, find and replace, the well-formedness check and
  text sanitizing. Same license position as above.

## Standards and reference material

- **W3C Speech Synthesis Markup Language (SSML) Version 1.0**: the document
  format. The scaffold written into new documents uses its namespace URI
  (`http://www.w3.org/2001/10/synthesis`). Azure's SSML support is documented
  by Microsoft; the SSML reference panel (a later phase) will link to it.
- **Extensible Markup Language (XML) 1.0**: the well-formedness rules the live
  check enforces (DTDs are refused; no entity is ever expanded).
- **The Unicode Standard**: normalization form C, the private-use ranges that
  are removed, and the Hebrew block and presentation forms that the Hebrew order
  repair recognises.

## Services

- **Azure AI Speech** (Microsoft) is called over HTTPS (REST) with the user's
  own key. It is a service, not a bundled library. Its use is governed by
  Microsoft's terms, including the Product Terms and the AI Services Code of
  Conduct. See the README for links and the author's intent about paid use.

## Evaluated and not used

- **Azure Speech SDK** (`Microsoft.CognitiveServices.Speech`): proprietary
  Microsoft license terms. Redistribution is limited to files on its
  REDIST.TXT list and requires end users to accept protective terms and the
  developer to indemnify Microsoft. Replaced by direct REST calls, which also
  give clean HTTP status codes.
- **TagLibSharp** (original TagLib#): LGPL-2.1. Kept only as a fallback if
  TagLibSharp2 proves unsuitable.
- **Older ScintillaNET packages** (`ScintillaNET`, `Scintilla.NET`): stale or
  deprecated. Use the `Scintilla5.NET` package ID only.
- **Stock RichTextBox**: no line-number margin, error indicators or fast XML
  colorization.
- **PdfPig `page.Text`** (extraction method, not a package): gives one line per
  page, which makes line-number correction notes useless. Replaced by
  `ContentOrderTextExtractor`, which keeps visual line breaks. Both produced the
  same characters in the same order on 66 pages of two study PDFs.

## Before the first binary release

- Include the Apache-2.0 license text for PdfPig, plus any NOTICE content
  shipped with it, and record the exact PdfPig version used.
- Include the MIT license and copyright notices for Scintilla5.NET and
  TagLibSharp2.
- Include the full Scintilla and Lexilla license notice (copy it from
  <https://www.scintilla.org/License.txt>), because that license requires the
  notice to accompany copies.
- Re-verify each license against the exact pinned package versions. This has not
  been done for PdfPig and Scintilla5.NET since phase 0.

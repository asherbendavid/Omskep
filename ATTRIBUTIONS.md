# Attributions

Third-party software and services used by Omskep, with license and source.
This file is updated by every phase as dependencies are added or removed.

Licenses below were checked against the package or project license text during
phase 0 (October 2026), and the test tooling again in phase 1. Re-check them when
versions are pinned.

## Libraries used or planned

| Component | Purpose | Introduced in | License | Source |
| --- | --- | --- | --- | --- |
| PdfPig | PDF text extraction | Phase 2 | Apache-2.0 (derived from Apache PDFBox) | <https://github.com/UglyToad/PdfPig>, NuGet package `PdfPig` |
| Scintilla5.NET 7.0.0 | WinForms editor control | Phase 2 | MIT | <https://www.nuget.org/packages/Scintilla5.NET> |
| Scintilla and Lexilla (native engine bundled inside Scintilla5.NET) | Text editing engine and XML lexer | Phase 2 | Scintilla license (short permissive notice), Copyright 1998-2021 Neil Hodgson | <https://www.scintilla.org/License.txt> |
| TagLibSharp2 0.6.0 | ID3v2 tags on exported MP3 files | Phase 3 | MIT | <https://github.com/decriptor/TagLibSharp2>, NuGet package `TagLibSharp2` |

Development and test only (not distributed with the application):

| Component | Purpose | License |
| --- | --- | --- |
| MSTest 4.0.2 (microsoft/testfx; meta package that pulls in MSTest.TestFramework, MSTest.TestAdapter, Microsoft.NET.Test.Sdk and the code-coverage extensions) | Unit tests | MIT |

The MSTest license was read on NuGet's page for the latest version (4.4.1); the
pinned 4.0.2 is the same project (microsoft/testfx). None of these test packages
is distributed with the application.

## Platform components (part of .NET, not separate dependencies)

- **Windows DPAPI** (`System.Security.Cryptography.ProtectedData`) protects the
  saved Azure key. It ships inside .NET 10 for Windows, so no package is
  referenced (NuGet reports NU1510 if one is added). Governed by the .NET
  runtime's MIT license. If Omskep is ever published self-contained, include
  the .NET runtime's license notices with the release.

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

## Before the first binary release

- Include the Apache-2.0 license text for PdfPig, plus any NOTICE content
  shipped with it.
- Include the MIT license and copyright notices for Scintilla5.NET and
  TagLibSharp2.
- Include the full Scintilla and Lexilla license notice (copy it from
  <https://www.scintilla.org/License.txt>), because that license requires the
  notice to accompany copies.
- Re-verify each license against the exact pinned package versions.

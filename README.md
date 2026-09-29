\# Omskep



Converts weekly Bible study PDFs (mixed Afrikaans/English) into narrated

MP3s using Azure AI Speech neural voices, for personal listening and study

group distribution.



\## Status



Phase 0 — project scaffold and technical decisions. Not yet functional.



\## Requirements



\- Windows 10 or later

\- .NET 10 runtime (framework-dependent deployment; not bundled)



\## Building from source



\- .NET 10 SDK

\- Visual Studio 2026 (or later) with the ".NET desktop development" workload

\- Open `Omskep.slnx`, or build from the command line: `dotnet build`



\## Project structure



\- `src/Omskep.Core` — SSML sanitization, chunking, billable-character

&#x20; counting, well-formedness checking (no UI or platform dependencies)

\- `src/Omskep.App` — WinForms application

\- `tests/Omskep.Core.Tests` — MSTest unit tests for Omskep.Core

\- `spike/` — throwaway exploration code, not part of the shipping app



\## License



BSD-3-Clause. See `LICENSE`.


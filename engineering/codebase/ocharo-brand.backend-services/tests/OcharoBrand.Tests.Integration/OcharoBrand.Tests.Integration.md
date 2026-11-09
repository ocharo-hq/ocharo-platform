# OcharoBrand.Tests.Integration

*Last updated: 2026-11-09*

Services resolved from the real host, below HTTP, and the SQLite import: `PostgresBusyTests` map a write that lost a race
to a conflict; `BrandRepositoryTests` race draft saves and consumer reads and keep instants; `LegacySqliteImportTests`
copy, verify, refuse and rerun the retired store's import; `MediaStoreTests` restore or re-upload a logo whose file was
deleted in WoW2 Files.

Prerequisites: the .NET 10 SDK and Docker. Each host or import gets a fresh PostgreSQL product database from
`OcharoBrand.Tests.Support`, a temp blob folder and legacy paths, and Files is `TestFiles`, an in-memory broker set; no
network or Identity instance is used. Run `dotnet test tests/OcharoBrand.Tests.Integration` from the backend solution
directory. The host harness is compiled from `../OcharoBrand.Tests.E2E/Harness`.

# OcharoBrand.Tests.Integration

*Last updated: 2026-11-09*

Services resolved from the real host, below HTTP: `ExceptionMapperTests` map SQLite busy and locked answers to a conflict;
`MediaStoreTests` restore or re-upload a logo whose file was deleted in WoW2 Files.

Prerequisites: none beyond the .NET 10 SDK. Each host gets a temp SQLite file, blob folder and key ring, and Files is
`TestFiles`, an in-memory broker set; no Docker, network or Identity instance is used. Run
`dotnet test tests/OcharoBrand.Tests.Integration` from the backend solution directory. The host harness is compiled
from `../OcharoBrand.Tests.E2E/Harness`.

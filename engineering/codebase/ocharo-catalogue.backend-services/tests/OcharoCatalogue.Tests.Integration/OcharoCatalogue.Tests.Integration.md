# OcharoCatalogue.Tests.Integration

*Last updated: 2026-11-09*

Catalogue commands and queries through the host's mediator and stores, below HTTP: `ProductServiceTests`,
`ProductPublicationTests`, `CatalogueDesignTests` (designs, releases, PDF checks, public media), `ImportExportTests`
and `FilesMediaStoreTests` (the media and release stores on WoW2 Files through `TestFiles`).

Prerequisites: none beyond the .NET 10 SDK. `CatalogueHarness` gives each test a temp SQLite file and blob folder, wired
like the host, with a `FakeTimeProvider` clock; Files is an in-memory broker set. No Docker, network or Identity instance is
used. Run `dotnet test tests/OcharoCatalogue.Tests.Integration` from the backend solution directory. The harness is
compiled from `../OcharoCatalogue.Tests.E2E/Harness`.

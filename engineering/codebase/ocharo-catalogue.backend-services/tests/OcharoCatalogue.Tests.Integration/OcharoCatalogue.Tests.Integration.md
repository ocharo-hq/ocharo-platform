# OcharoCatalogue.Tests.Integration

*Last updated: 2026-11-09*

Catalogue commands and queries through the host's mediator and stores, below HTTP: `ProductServiceTests`,
`ProductPublicationTests`, `CatalogueDesignTests` (designs, releases, PDF checks, public media), `ImportExportTests`,
`FilesMediaStoreTests` (the media and release stores on WoW2 Files through `TestFiles`), `LegacySqliteImportTests` (the
one-time SQLite copy: verified copy and retire, dry run, refusal on a diverged row, rerun, a store made before Files) and
`CatalogueMigrationSetTests` (the baseline's Apply and Rollback rerun and invert, history bypassed).

Prerequisites: the .NET 10 SDK and Docker. `CatalogueHarness` gives each test a fresh PostgreSQL database (from
`CatalogueTestDatabase`'s migrated template) and a temp blob folder, wired like the host, with a `FakeTimeProvider` clock;
Files is an in-memory broker set. No network or Identity instance is used. Run `dotnet test tests/OcharoCatalogue.Tests.Integration` from the backend solution directory. The harness is
compiled from `../OcharoCatalogue.Tests.E2E/Harness`.

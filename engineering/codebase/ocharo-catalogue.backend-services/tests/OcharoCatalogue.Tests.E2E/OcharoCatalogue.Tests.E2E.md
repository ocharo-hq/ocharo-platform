# OcharoCatalogue.Tests.E2E

*Last updated: 2026-11-09*

Whole HTTP flows through the real host: `CatalogueAccessTests` (sign-in, organization and permission checks, media access,
headers, boot refusals), `CatalogueApiTests` (seeding, revision-checked edits, uploads, import), `CatalogueDesignApiTests`,
`CatalogueWireContractTests`, `ProblemDetailsContractTests`, `RuntimeConfigTests`, `SystemStatusTests`,
`SettingsFilesTests`, `FilesMediaRouteTests` (the media route on WoW2 Files) and `LegacySqliteImportVerbTests` (the 503
gate and `import-sqlite` over the host's services, ending on the published `wave1-check` edition and its PDF).

Prerequisites: the .NET 10 SDK and Docker. `CatalogueTestDatabase` starts one PostgreSQL 18 container per run and gives
each host a fresh database copied from a template the host's own sets were applied to, plus a temp blob folder; sign-in
is `CatalogueSignIn`, a stand-in for the Identity BFF cookie scheme driven by the `X-Test-Person` header, so no call
reaches Identity or Files. Run `dotnet test tests/OcharoCatalogue.Tests.E2E` from the backend solution directory. `Harness/` holds
the shared hosts, test database, sign-in, Files stand-in, mediator wrappers, the published-edition fixture and the
writer of a retired SQLite store (`LegacyCatalogueStore`).

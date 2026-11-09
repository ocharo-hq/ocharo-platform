# OcharoCatalogue.Tests.E2E

*Last updated: 2026-11-09*

Whole HTTP flows through the real host: `CatalogueAccessTests` (sign-in, organization and permission checks, media access,
headers, boot refusals), `CatalogueApiTests` (seeding, revision-checked edits, uploads, import), `CatalogueDesignApiTests`,
`CatalogueWireContractTests`, `ProblemDetailsContractTests`, `RuntimeConfigTests`, `SystemStatusTests`,
`SettingsFilesTests` and `FilesMediaRouteTests` (the media route on WoW2 Files).

Prerequisites: none beyond the .NET 10 SDK. Each host gets a temp SQLite file and blob folder; sign-in is `CatalogueSignIn`,
a stand-in for the Identity BFF cookie scheme driven by the `X-Test-Person` header, so no call reaches Identity or Files.
No Docker needed. Run `dotnet test tests/OcharoCatalogue.Tests.E2E` from the backend solution directory. `Harness/` holds
the shared hosts, sign-in, Files stand-in and mediator wrappers.

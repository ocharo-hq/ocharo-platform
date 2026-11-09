# OcharoBrand.Tests.E2E

*Last updated: 2026-11-09*

Whole HTTP flows through the real host: `AccessTests` (sign-in, organization and permission checks, CSRF, headers, boot
refusals), `BrandApiTests` (seed, publishing, appearances, identity edits, uploads, problem details),
`RuntimeConfigTests`, `SystemStatusTests`, `SettingsFilesTests` (SDK settings files, OpenAPI on a local run),
`FilesMediaTests` (Brand on WoW2 Files through `TestFiles`), `LegacySqliteImportVerbTests` (503 until `import-sqlite`
copies a retired store, then release 1 and the draft as before) and `SessionStoreTests` (sessions and key ring survive a
restart).

Prerequisites: the .NET 10 SDK and Docker. Each host gets a fresh PostgreSQL product database from
`OcharoBrand.Tests.Support`, a temp blob folder and legacy paths; sign-in is `BrandTestIdentity`, a stand-in for the
Identity BFF cookie scheme driven by the `X-Test-Person` header, so no call reaches Identity or Files. Run
`dotnet test tests/OcharoBrand.Tests.E2E` from the backend solution directory. `Harness/` holds the shared hosts,
identity and Files stand-in.

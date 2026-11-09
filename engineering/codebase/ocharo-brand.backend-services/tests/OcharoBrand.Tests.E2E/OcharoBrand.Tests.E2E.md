# OcharoBrand.Tests.E2E

*Last updated: 2026-11-09*

Whole HTTP flows through the real host: `AccessTests` (sign-in, organization and permission checks, CSRF, headers, boot
refusals), `BrandApiTests` (seed, publishing, appearances, identity edits, uploads, problem details),
`RuntimeConfigTests`, `SystemStatusTests`, `SettingsFilesTests` (SDK settings files, OpenAPI on a local run) and
`FilesMediaTests` (Brand on WoW2 Files through `TestFiles`).

Prerequisites: none beyond the .NET 10 SDK. Each host gets a temp SQLite file, blob folder and key ring; sign-in is
`BrandTestIdentity`, a stand-in for the Identity BFF cookie scheme driven by the `X-Test-Person` header, so no call
reaches Identity or Files. No Docker needed. Run `dotnet test tests/OcharoBrand.Tests.E2E` from the backend solution
directory. `Harness/` holds the shared hosts, identity and Files stand-in.

# ventures.ocharo-brand — Backend Development Guidelines

*Last updated: 2026-11-09*

Defer to wow-two-ws/conventions/development/backend/. Requests run through the be sdk's mediator: commands and queries in
`OcharoBrand.Application/Brand/{Commands,Queries}`, their handlers in `OcharoBrand.Infrastructure/Brand/{CommandHandlers,QueryHandlers}`
(shared reads in `FoundationServices/`), and both controllers inject only `ISender`. Handlers return `AppResult`; failures
come from `BrandErrors` and render as SDK problem details (`code` names the kind, `brandCode` the Brand refusal), with
`BrandErrorHttpStatusCodeMapper` keeping the 413, 415, 502 and 503 (`storage_moving`) no SDK kind carries and
`PostgresBusyExceptionMappingRule` the 409 on a write that lost a race. Controllers use `AddControllersWithSdkJson`
(nulls omitted): the private API answers in the `{ data }` envelope; the public kit and runtime config stay bare, their
keys pinned. Repo deltas: revision-checked draft saves, lazily resolved settings (connection string, blob root, legacy
paths) so hosts/tests can override them.

Storage: PostgreSQL schema `brand` of the product database, owned by the bespoke SQL set in
`OcharoBrand.Persistence/Migrations/` (embedded; `Apply.sql` and `Rollback.sql` per `NNN-name/`). A schema change is a
new numbered folder, never an edit of an applied one, and the EF model follows the SQL (snake_case names, no
`EnsureCreated`, no EF migrations). The `migrate` verb applies the sets; `import-sqlite [--dry-run]` copies the retired
SQLite store once (`../../deployment/brand/brand.md` § PostgreSQL move).

Local run: the `https` launch profile sets `LOCAL_SETTINGS=true`, so the be sdk reads a gitignored
`OcharoBrand.Api/appsettings.Local.json` holding this machine's `ConnectionStrings:Database` and `Brand:SeedLogoRoot`;
without the seed folder, an empty store seeds without logos. A local run creates the database if missing and applies
both sets at boot. Point it at the local product database, Desk's packaged Compose `postgres` on `127.0.0.1:8270`
(database `brand_workspace`; Brand takes schemas `brand` and `brand_cache` beside Desk's), with the password in the
ignored file only:

```json
{ "ConnectionStrings": { "Database": "Host=127.0.0.1;Port=8270;Database=brand_workspace;Username=brand_workspace;Password=<local password>" } }
```

While the ignored `App_Data/ocharo-brand.db` exists, brand routes answer 503 until `dotnet run --project OcharoBrand.Api
--no-build --launch-profile https -- import-sqlite` copies it (add `--dry-run` first to see the plan). OpenAPI is served
on a local run only.

Runtime config: `RuntimeConfig/` serves `GET /api/runtime-config` from the optional `OcharoApps:{Desk,Catalogue,Studio,
Marketing,Editorial,Brand,Atlas,Wear,Blog}` settings as `OCHARO_*_URL` keys, the same agreement as Desk and Catalogue.
Every address is optional; a value that is not a plain absolute HTTPS address is left out and named in one startup
warning. `SystemStatus/` serves `GET /api/system/status`, anonymous,
`no-store` and listed in `HostConfiguration.Sso.cs` as public SSO requests.

Tests live in `codebase/ocharo-brand.backend-services/tests/` as `OcharoBrand.Tests.Unit`, `.Integration` and `.E2E`; their hosts
use `BrandHost` or `FilesWorkspace` (in `OcharoBrand.Tests.E2E/Harness/`): a fresh PostgreSQL product database from
`OcharoBrand.Tests.Support` (one `postgres:18-alpine` container per run, Docker required, a migrated template copied per
test), a temp blob folder and legacy paths, a missing seed folder, and the test identity that stands in for the
Identity BFF. Never point a test or a browser check at `App_Data/` or a shared database.

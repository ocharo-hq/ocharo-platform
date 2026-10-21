# ventures.ocharo-brand — Backend Development Guidelines

*Last updated: 2026-10-21*

Defer to wow-two-ws/conventions/development/backend/. Repo deltas: application services (not mediator verticals), `BrandException` → ProblemDetails, revision-checked draft saves, lazily resolved paths (connection string and blob root) so hosts/tests can override them.

Local run: the `https` launch profile sets `LOCAL_SETTINGS=true`, so the be sdk reads a gitignored
`OcharoBrand.Api/appsettings.Local.json` holding this machine's `Brand:SeedLogoRoot`; without it, an empty store seeds
without logos. OpenAPI is served on a local run only.

Runtime config: `RuntimeConfig/` serves `GET /api/runtime-config` from the optional `OcharoApps:{Desk,Catalogue,Studio,
Marketing,Editorial,Brand,Atlas,Wear,Blog}` settings as `OCHARO_*_URL` keys, the same agreement as Desk and Catalogue.
Every address is optional; a value that is not a plain absolute HTTPS address is left out and named in one startup
warning. `SystemStatus/` serves `GET /api/system/status`; `/api/system/version` is retained. Both are anonymous,
`no-store` and listed in `HostConfiguration.Sso.cs` as public SSO requests.

Tests use `BrandHost` (in `RuntimeConfigTests.cs`): a temp SQLite file and blob folder, a missing seed folder, and
optional enabled SSO. Never point a test or a browser check at `App_Data/`.

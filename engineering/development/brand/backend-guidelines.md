# ventures.ocharo-brand — Backend Development Guidelines

Defer to wow-two-ws/conventions/development/backend/. Repo deltas: application services (not mediator verticals), `BrandException` → ProblemDetails, revision-checked draft saves, lazily resolved paths (connection string and blob root) so hosts/tests can override them.

Local run: the `https` launch profile sets `LOCAL_SETTINGS=true`, so the be sdk reads a gitignored
`OcharoBrand.Api/appsettings.Local.json` holding this machine's `Brand:SeedLogoRoot`; without it, an empty store seeds
without logos. OpenAPI is served on a local run only.

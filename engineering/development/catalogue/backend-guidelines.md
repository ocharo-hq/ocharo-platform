# ventures.ocharo-catalogue — Backend Development Guidelines

Defer to wow-two-ws/conventions/development/backend/. Repo deltas: application services (not mediator verticals) for the document-style catalogue, `CatalogueException` → ProblemDetails, revision-checked saves, lazily resolved paths (connection string and blob root) so hosts/tests can override them.

Local run: the `https` launch profile sets `LOCAL_SETTINGS=true`, so the be sdk reads a gitignored
`OcharoCatalogue.Api/appsettings.Local.json` holding this machine's `Catalogue:SourceRoot`; without it, Import reports no
configured source folder and an empty store skips seeding. OpenAPI is served on a local run only.

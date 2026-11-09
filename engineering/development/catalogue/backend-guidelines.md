# ventures.ocharo-catalogue — Backend Development Guidelines

Defer to wow-two-ws/conventions/development/backend/. Requests dispatch through the SDK mediator: Application holds the
commands, queries and models, Infrastructure the handlers, which return `AppResult` failures from `CatalogueErrors`;
deep validation (product input, design snapshots, artifacts) throws `AppException` and the SDK's exception-to-result
interceptor returns it as the failure. Repo deltas: revision-checked saves, lazily resolved paths (connection string and
blob root) so hosts/tests can override them, data-keyed dictionaries serialized with their keys as stored, and the
public feed's frozen wire shape (`Publishing/Serializers/PublicFeedJson.cs`).

Storage: PostgreSQL schema `catalogue` of the product database (`ConnectionStrings:Database`), shared with Desk,
Identity and Studio. The bespoke SQL set `OcharoCatalogue.Persistence/Migrations/` owns it; EF Core only maps it, so a
schema change is a new `NNN-name/{Apply,Rollback}.sql` folder, never an EF migration or `EnsureCreated`. A local run
applies pending migrations at boot; a deployed boot only verifies them, and the deploy's `migrate` step applies them.
BFF sessions live in schema `catalogue_cache`, the Data Protection key ring in `catalogue.data_protection_keys`. The
retired SQLite store is read once by `import-sqlite` (deployment `catalogue.md` § PostgreSQL move). Integration and E2E
tests need Docker.

Local run: the `https` launch profile sets `LOCAL_SETTINGS=true` and trusts the Vite dev server's origin
(`Catalogue__TrustedOrigins__0`), and the be sdk reads a gitignored `OcharoCatalogue.Api/appsettings.Local.json` holding
this machine's `ConnectionStrings:Database` (a local PostgreSQL; a local run creates the database), `Catalogue:SourceRoot`,
`Catalogue:OrganizationId` and `Identity:Client`; without a source folder, Import reports none and an empty store skips
seeding. An `App_Data/ocharo-catalogue.db` left from the SQLite release holds catalogue routes at 503 until
`dotnet run --project OcharoCatalogue.Api --launch-profile https -- import-sqlite` copies it with the same settings. OpenAPI
is served on a local run only.

Runtime config: optional `OcharoApps:{Desk,Catalogue,Studio,Marketing,Editorial,Brand,Atlas,Wear,Blog}` settings feed
`GET /api/runtime-config`; each is an absolute HTTPS address, and a malformed one is left out with a startup warning.
`GET /api/system/status` reports the informational version.

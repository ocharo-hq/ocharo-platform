# ventures.ocharo-catalogue — Backend Development Guidelines

Defer to wow-two-ws/conventions/development/backend/. Requests dispatch through the SDK mediator: Application holds the
commands, queries and models, Infrastructure the handlers, which return `AppResult` failures from `CatalogueErrors`;
deep validation (product input, design snapshots, artifacts) throws `AppException` and the SDK's exception-to-result
interceptor returns it as the failure. Repo deltas: revision-checked saves, lazily resolved paths (connection string and
blob root) so hosts/tests can override them, data-keyed dictionaries serialized with their keys as stored, and the
public feed's frozen wire shape (`Publishing/Serializers/PublicFeedJson.cs`).

Local run: the `https` launch profile sets `LOCAL_SETTINGS=true`, so the be sdk reads a gitignored
`OcharoCatalogue.Api/appsettings.Local.json` holding this machine's `Catalogue:SourceRoot`; without it, Import reports no
configured source folder and an empty store skips seeding. OpenAPI is served on a local run only.

Runtime config: optional `OcharoApps:{Desk,Catalogue,Studio,Marketing,Editorial,Brand,Atlas,Wear,Blog}` settings feed
`GET /api/runtime-config`; each is an absolute HTTPS address, and a malformed one is left out with a startup warning.
`GET /api/system/status` reports the informational version.

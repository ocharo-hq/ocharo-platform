# Backend foundation

*Last updated: 2026-10-21*

.NET 10 API and independent worker, with PostgreSQL-owned technical diagnostics.
The API exposes no business mutation and starts no job processor. Authentication,
organization authorization and Content & Publishing remain subsequent slices.

## Build

```sh
dotnet restore BrandWorkspace.BackendServices.slnx --locked-mode -m:1
dotnet build BrandWorkspace.BackendServices.slnx --no-restore -m:1
dotnet test BrandWorkspace.BackendServices.slnx --no-build
```

The normal API build runs `pnpm install --frozen-lockfile` and `pnpm build` in the sibling frontend workspace.
Vite supplies generated API `wwwroot` files. `BuildSpa=false` is for backend-only
verification or a container stage that already supplied the verified SPA output.
Tests use the published SDK HTTP harness and an isolated PostgreSQL container;
Docker is required. They do not use or restart the shared development database.

## Runtime commands

Desk needs one PostgreSQL database. The local endpoint is `127.0.0.1:8270` (the packaged
Compose `postgres` service); database and user are `brand_workspace`. Give the API the complete
connection string with its password, either as `ConnectionStrings__Database` or in an ignored
`BrandWorkspace.Api/appsettings.Local.json`, which only the `https` launch profile reads:

```json
{ "ConnectionStrings": { "Database": "Host=127.0.0.1;Port=8270;Database=brand_workspace;Username=brand_workspace;Password=<local password>" } }
```

Passwords never go in committed settings. A local run keeps its Data Protection keys under the user's
application-data folder unless `DataProtection:KeysPath` names one; any other run requires it.

From this directory, after a build:

```sh
dotnet run --project BrandWorkspace.Api --no-build --launch-profile https
dotnet run --project BrandWorkspace.Api --no-build --no-launch-profile -- --migrate
dotnet run --project BrandWorkspace.Worker --no-build --no-launch-profile
```

The API owns the schema. Serving applies pending embedded SQL migrations (SDK checksum
journal, PostgreSQL advisory lock `8264001`) and the provider-owned Hangfire schema before it
listens, so `/health/ready` never precedes them. An unreachable database is retried ten times,
two seconds apart; any other migration failure aborts startup. `--migrate` applies the same
steps and exits. The worker never changes schema and refuses to start before it exists.
The API serves HTTPS `8264` only (Ocharo's HTTPS-only override; `8265` stays reserved);
Docker explicitly supplies `ASPNETCORE_URLS=http://+:8080`. Worker has no port.

Application environment configuration permits `ConnectionStrings__Database`, plus
API `ASPNETCORE_URLS` and `AllowedHosts`. A hosted deployment supplies the exact
host list, such as `<public-host>;localhost;127.0.0.1`; the local default is restrictive.
Runtime/framework environment selection occurs during host
construction. Arbitrary ambient variables are removed from application configuration.

## Settings files and local runs

Both hosts read their settings files through the SDK's `AddSettingsFiles`: `appsettings.json`,
`appsettings.{Environment}.json`, then a deployed target's mounted `appsettings.Target.json`, then
`appsettings.Local.json` only on a local run whose launch profile sets `LOCAL_SETTINGS=true`.
`DEPLOY_ENVIRONMENT` (set by the deploy runner) and `LOCAL_SETTINGS` are host-bootstrap inputs read from
the process, not application aliases. A run is local only in `Development` without `DEPLOY_ENVIRONMENT`;
Telegram polling, the Data Protection key fallback and the Commerce source-root fallback stay local-only.
Tests and containers never read the machine file. The worker has no launch profile: set
`DOTNET_ENVIRONMENT=Development` and `LOCAL_SETTINGS=true` for it to read its own `appsettings.Local.json`.

Deployed targets run as `Production`. The API trusts `X-Forwarded-For`, `-Proto` and `-Host` only from
loopback and the addresses in `Deployment:TrustedProxies` (a JSON array; default empty). On the shared VPS
the target sets it to the ingress, `["172.30.0.2"]`, so sign-in sees HTTPS and the public host.

## Harmless durable probe

```sh
dotnet run --project BrandWorkspace.Worker --no-build --no-launch-profile -- --enqueue-probe
dotnet run --project BrandWorkspace.Worker --no-build --no-launch-profile -- --probe-status JOB_ID
```

Enqueue emits one JSON object containing `jobId` and `probeId`. Status emits `jobId`,
`probeId`, Hangfire `state`, and `completedAtUtc`. A missing/expired job returns
`state: missing` and exit code `2`; a recognized job returns exit code `0` even while
pending. Completion requires `state: Succeeded` and a non-null receipt timestamp.
Successful job details follow Hangfire's default retention, so this is a diagnostic,
not a permanent business audit. Invalid arguments return `2`; operational failure
returns nonzero through the CLI exception path.

CLI composition registers a client without any processing server. Only the normal
worker consumes the `foundation` queue. The job writes an idempotent PostgreSQL
receipt, with three automatic retry attempts; it never contacts a provider.
Restart proof: stop the worker, enqueue, restart PostgreSQL preserving its volume,
start the worker, then inspect the same job ID and receipt. Keeping the API alive
does not consume pending work.

## HTTP contract

- `GET /health/live`: process liveness, independent of database availability.
- `GET /health/ready`: connectivity and expected application/queue schema; HTTP `503`
  problem JSON when unavailable. No schema creation occurs during this check.
- `GET /api/system/status`: HTTP `200` with this schema when persistence is ready:

```json
{
  "service": "brand-workspace",
  "stage": "foundation",
  "persistence": "PostgreSQL",
  "worker": { "state": "waiting", "lastSeenAtUtc": null }
}
```

The worker stores a heartbeat every five seconds. No heartbeat means `waiting`;
one within 30 seconds means `healthy`; an older or future timestamp means `stale`.
This is worker-process liveness, not provider readiness or proof that a particular
job succeeded. A database outage produces HTTP `503`, never a fabricated heartbeat.
Unknown `/api` paths, including dotted paths and non-GET methods, return JSON `404`.

- `GET /api/runtime-config`: the public settings the SPA reads once at startup (`RuntimeConfigController`, the
  only MVC controller). Anonymous, `Cache-Control: no-store`, and never a secret. It answers a flat JSON object of
  the configured `OcharoApps:*` addresses under the SPA's config keys; an unset or malformed address is left out,
  and nothing configured answers `{}`:

```json
{
  "OCHARO_DESK_URL": "https://ocharo-desk-dev.<tailnet>.ts.net/",
  "OCHARO_CATALOGUE_URL": "https://ocharo-catalogue-dev.<tailnet>.ts.net/catalogues"
}
```

The keys are `OCHARO_{DESK,CATALOGUE,STUDIO,MARKETING,EDITORIAL,BRAND,ATLAS,WEAR,BLOG}_URL`, from the settings
`OcharoApps:{Desk,Catalogue,Studio,Marketing,Editorial,Brand,Atlas,Wear,Blog}` ([target settings](../../deployment/deployment.md#target-settings)).
A value is served only as an absolute HTTPS address without credentials, query or fragment; the host logs one startup
warning naming each setting it leaves out. The SPA layers these values ahead of its build-time `VITE_OCHARO_*_URL`
values and local `localhost` defaults.

### App sign-in (SSO)

Desk issues delegated access to Ocharo apps through `/api/sso/{authorize,exchange,refresh,revoke}`; the protocol,
trust configuration and limits are in the [Ocharo SSO doc](../ocharo-sso/README.md).

- `POST /api/integrations/introspect` (`IntegrationTickets`): anonymous backchannel over HTTPS, `no-store`. For an
  audience-bound ticket whose Desk session and organization membership are still live it answers
  `{ data: { active: true, userId, organizationId, role, canEdit, canApprove, expiresAt, displayName, email } }`;
  anything else answers `{ data: { active: false } }` with no profile.
- `displayName` and `email` are read from `access.users` on each introspection (`IAccessRepository.FindProfileAsync`),
  so they are the ticket holder's own current profile. A blank name falls back to the email; a missing user or value
  is `null`. The exchange and refresh bodies stay credential-only; apps receive identity only through introspection.
- No schema change: `DisplayName` and `Email` already exist on `access.users` (migration `002-access`).

## SDK adoption boundary

Published `WoW2.Sdk.Beta.Be` and `WoW2.Sdk.Beta.Be.Testing` are pinned to `10.0.71-beta`
(settings files and the local-run check); NuGet availability was verified on 19 October 2026.
The family was renamed from `WoW2.Sdk.Backend.Beta` at `10.0.67-beta`.

The worker calls public SDK `AddHangfireJobs` with explicit PostgreSQL storage
options (`PrepareSchemaIfNecessary=false`). The SDK's PostgreSQL preset auto-creates
schema and its jobs registration always includes a server. Therefore CLI-only
submission uses upstream `AddHangfire` at the host composition boundary. This is
an SDK client-only/schema-control preset gap; no product scheduler abstraction or
private SDK API was introduced. Shared SDK files were not changed.

Application schema migration uses public `AddDatabaseBespokeMigrations(assembly)`
and `AddDataSourceConnectionFactory`. The API alone installs the Hangfire schema, through
Hangfire's own idempotent installer, without registering a job client. No EF model competes
for schema ownership, so the SDK's EF startup runner does not apply; the SDK's bespoke startup
helper needs `AddPostgresPersistence`, so the host calls the same runner directly.
Forwarded headers use the SDK's `AddProxyAwareHosting`/`UseProxyAwareHosting`.

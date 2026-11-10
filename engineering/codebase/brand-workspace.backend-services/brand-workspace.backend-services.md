# Backend foundation

*Last updated: 2026-11-09*

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

Committed `appsettings.json` holds no connection string or allowed hosts: a deployed target gets both from
Wheelhouse, and a local run from the ignored local file. Passwords never go in committed settings. A local run keeps its Data Protection keys under the user's
application-data folder unless `DataProtection:KeysPath` names one; any other run requires it.

From this directory, after a build:

```sh
dotnet run --project BrandWorkspace.Api --no-build --launch-profile https
dotnet run --project BrandWorkspace.Api --no-build --no-launch-profile -- migrate
dotnet run --project BrandWorkspace.Worker --no-build --no-launch-profile
```

`Program.cs` hands its arguments to the be sdk's `ServiceHost.RunAsync`: a first argument that names a verb runs it on the
built host without serving, and its exit code is the process's; anything else serves.

| Verb | Does |
|---|---|
| `migrate` | the be sdk's: applies every migration set (Desk's own, then the session cache's `distributed_cache`) and exits; `migrate check`, `plan` and the rest as the SDK documents |
| `config check` | the be sdk's: checks the settings as a boot would and exits `0` consistent, `1` refused, `2` failed |
| `import sourcing` / `import events` | imports a bundle as an editor: `--bundle <bundle.json> --organization <organization-id> --actor <identity-user-id\|email> [--dry-run]`; the actor is a person Desk projected from Identity (by `sub`, or by an email one person carries) and the organization one Desk knows, else it refuses; exits `0`, `1` refused, `2` with failures |
| `export events` | writes an organization's events as a WoW2 Events import bundle (Events' `wow2.events-import.v1`, [events-adoption.md](../../research/events-adoption.md) #6): `--organization <organization-id> --out <bundle.json>`; reads Desk, writes only the new file, refuses an existing one or an organization Desk never saw; prints the counts by stage and precision; exits `0`, `1` refused |
| `media verify` | decodes a fixed PNG through the packaged native decoder, with no database or key ring; the image build runs it |

A verb's stdout carries only its own output; the host's logs go to stderr.

The API owns Desk's schema. Serving applies pending embedded SQL migrations (SDK checksum
journal, PostgreSQL advisory lock `8264001`) before it listens, deployed included, so `/health`
never reports ready ahead of them. An unreachable database is retried ten times, two seconds apart;
any other migration failure aborts startup. The `migrate` verb applies the same sets and exits.
The worker never applies Desk's schema and refuses to start before it exists; the executing worker
installs the job queue's own schema (Hangfire's idempotent installer) as it builds its storage.
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

The host boots on the be sdk's `AddApiDefaults`/`UseApiDefaults` (service name `brand-workspace`): Serilog, proxy-aware
hosting, the secure headers, compression, the SPA bundle and its fallback, and `/health`. OTLP export, rate limiting,
output caching, the embedder policy and the HTTPS redirect stay off, as before. Every response carries the SDK's secure
headers, `Cross-Origin-Resource-Policy: same-origin` included; the public brand routes (`/api/public/v1/brands/{slug}/…`)
answer `cross-origin` instead, because Blog and Marketing pages embed their media and edition files.

Every route is a sealed controller under its domain's `Controllers/` that sends the mediator's commands and queries
(Application) to handlers (Infrastructure) returning `AppResult<T>`. Organization routes run `[OrganizationAccess]`, which
resolves the member's access from the Identity claims and projects them into Desk. Answers are `{ "data": … }` on the SDK
JSON (`AddControllersWithSdkJson`: camelCase, null members left out, enums as camelCase strings); failures are the SDK's
problem details, `detail` the message and `code` the error kind (`Validation`, `NotFound`, `Conflict`, `Forbidden`, …).
Desk's status mapper answers 413 for an oversized body, 415 for an unsupported image type and 502 for a call WoW2 Files
refused. Dictionary keys travel as written, so size labels such as `XL` keep their case; the commerce draft reads
`{}` before its first save, and the public brand routes answer their payload bare, as Marketing and Blog read it.
Commerce's stored snapshots keep their own JSON (`CommerceJson`: nulls written). Below the handlers, validators and repositories raise catalog errors
(`DeskErrors`) that the mediator's exception-mapping interceptor returns as failures.

- `GET /health`: the boot bundle's endpoint, anonymous: `200 Healthy` once PostgreSQL answers and Desk's
  schema is in place, else `503 Unhealthy`. No schema creation occurs during this check.
- `GET /api/system/status` (`SystemController`): HTTP `200` with this schema when persistence is ready, `503`
  problem JSON otherwise; `version` is the informational version:

```json
{
  "service": "brand-workspace",
  "stage": "foundation",
  "persistence": "PostgreSQL",
  "worker": { "state": "waiting" },
  "version": "0.4.2+0123456789abcdef0123456789abcdef01234567"
}
```

`worker.lastSeenAtUtc` is left out while no heartbeat exists.

The worker stores a heartbeat every five seconds. No heartbeat means `waiting`;
one within 30 seconds means `healthy`; an older or future timestamp means `stale`.
This is worker-process liveness, not provider readiness or proof that a particular
job succeeded. A database outage produces HTTP `503`, never a fabricated heartbeat.
Unknown `/api` paths, including dotted paths and non-GET methods, return JSON `404`.

- `GET /api/runtime-config`: the public settings the SPA reads once at startup (`RuntimeConfigController`). Anonymous, `Cache-Control: no-store`, and never a secret. It answers a flat JSON object of
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

### App sign-in

Desk signs in through Identity's BFF (`UseIdentityBff`) and issues no delegated tickets: every Ocharo app signs in
through Ocharo's Identity on its own.

- Desk keeps no accounts, sessions or memberships: `[OrganizationAccess]` reads the role from the claims and projects the
  person and organization into `access.users` and `access.organizations`, which the records' foreign keys need.
- Migration `013-identity-cutover` (a contract, `since` `v0.2.7`) dropped Desk's own sign-in storage with its rows:
  `access.sessions`, `memberships`, the `sso_*` and `telegram_*` tables and the six EF `identity_*` tables. The `011`
  reserved for it stays a gap: deployed databases already held `012-files`, and the migrator refuses a pending ordinal
  below the applied level outside a local run.

- Background removal (`/api/integrations/media/background-removal` and `/capabilities`) takes only an Identity service token,
  verified offline against the JWKS of `Identity:Client:Authority`: audience `Identity:Resource:Uri` (required at boot), scope
  `desk.remove-background`. Bad token or none `401`, missing scope `403`; the BFF cookie stays the default scheme and never opens it.

## Media in WoW2 Files

With `Files:BaseUrl` set, Desk's 4 byte stores keep their bytes in WoW2 Files, tenant `ocharo`, as app `desk`
(`engineering/research/files-adoption.md` #9–#15). Each upload's idempotency key is its reference, the same the
migration manifest (`engineering/deployment/files-migration.yml`, wave 3) gives the files it moves:

| Rows | Reference | Route |
|---|---|---|
| `preparation.media` | `desk.preparation-media:{organization_id}/{id}` | `/media/{id}/content` answers bytes, read through Files: the design export `fetch()`es it |
| `preparation.exports` | `desk.preparation-export:{organization_id}/{id}`, a private ZIP | `/exports/{id}/download` redirects to a link saved as `manual-content-{id}.zip` |
| `commerce.media` | `desk.commerce-media:{brand_id}/{sha256}` | the member route answers bytes (design export); the public media and edition assets redirect |
| `sourcing.media_contents` | `desk.sourcing-media:{organization_id}/{sha256}` | content redirects; the thumbnail redirects to the 480 px variant, and no thumbnail bytes are stored |

- every route keeps its own access decision and its `no-store`; a link lives 15 minutes.
- migration `012-files` adds `file_id` beside each `bytes` column and drops its `NOT NULL`. The immutable preparation rows
  never change: a row stored before Files gets its file id in an insert-only `media_files` or `export_files` record.
- the commerce attach references the preparation image's file instead of copying its bytes.
- the boot fills file ids for rows stored before Files: a `desk` file in Files with the row's SHA-256 and its own
  reference. Until then the row reads its PostgreSQL bytes; an unreachable Files leaves the boot running.
- a person's request calls Files with their exchanged token (the BFF session); the `import sourcing` verb, the boot and
  anonymous routes call it as Desk, by client credentials of `Identity:Client`. The worker touches no media.
- with `Files:BaseUrl` unset, every byte stays in PostgreSQL as before.

## SDK adoption boundary

Published `WoW2.Sdk.Beta.Be` and `WoW2.Sdk.Beta.Be.Testing` are pinned to `10.0.106-beta`, Files' `Abstractions` and
`Client` to `10.0.5-beta`, and the Identity packages to `10.0.30-beta`.
The family was renamed from `WoW2.Sdk.Backend.Beta` at `10.0.67-beta`.

The worker calls public SDK `AddHangfireJobs` with explicit PostgreSQL storage
options (`PrepareSchemaIfNecessary` only for the executing worker). The SDK's PostgreSQL preset auto-creates
schema and its jobs registration always includes a server. Therefore CLI-only
submission uses upstream `AddHangfire` at the host composition boundary. This is
an SDK client-only/schema-control preset gap; no product scheduler abstraction or
private SDK API was introduced. Shared SDK files were not changed.

Application schema migration uses public `AddDatabaseBespokeMigrations(assembly)`
and `AddDataSourceConnectionFactory`. The executing worker installs the Hangfire schema, through
Hangfire's own idempotent installer; the API registers no job client. No EF model competes
for schema ownership, so the SDK's EF startup runner does not apply; the SDK's bespoke startup
helper needs `AddPostgresPersistence`, so the host calls the same runner directly.
Forwarded headers come from the boot bundle, trusting loopback and `Deployment:TrustedProxies`. The Data Protection
key ring uses the SDK's `AddPersistentDataProtection`: application name `DataProtection:ApplicationName`, else
`brand-workspace:{Environment}`; key directory `DataProtection:KeysPath`, else, on a local run only, the user's
application-data folder `brand-workspace/{Environment}/keys`.

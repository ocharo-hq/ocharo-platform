# Backend foundation

.NET 10 API and independent worker, with PostgreSQL-owned technical diagnostics.
The API exposes no business mutation and starts no job processor. Authentication,
organization authorization and Content & Publishing remain subsequent slices.

## Build

```sh
dotnet restore BrandWorkspace.BackendServices.slnx --locked-mode -m:1
dotnet build BrandWorkspace.BackendServices.slnx --no-restore -m:1
dotnet test BrandWorkspace.BackendServices.slnx --no-build
```

The normal API build runs `npm ci` and `npm run build` in the sibling frontend.
Vite supplies generated API `wwwroot` files. `BuildSpa=false` is for backend-only
verification or a container stage that already supplied the verified SPA output.
Tests use the published SDK HTTP harness and an isolated PostgreSQL container;
Docker is required. They do not use or restart the shared development database.

## Runtime commands

Set `ConnectionStrings__Database` to the complete PostgreSQL connection string.
The local endpoint is `127.0.0.1:8270`; database and user are `brand_workspace`.
Passwords belong in environment configuration, never committed settings.

From this directory, after a build:

```sh
dotnet run --project BrandWorkspace.Api --no-build --no-launch-profile -- --migrate
dotnet run --project BrandWorkspace.Api --no-build --launch-profile https
dotnet run --project BrandWorkspace.Worker --no-build --no-launch-profile
```

`--migrate` is the only application migration owner. It applies embedded versioned
SQL with the SDK checksum journal and PostgreSQL advisory lock, initializes the
provider-owned Hangfire schema, then exits. Neither normal host changes schema.
The deployment runs one migration applicant. API ports are HTTPS `8264` and HTTP
`8265`; Docker explicitly supplies `ASPNETCORE_URLS=http://+:8080`. Worker has no port.

Application environment configuration permits `ConnectionStrings__Database`, plus
API `ASPNETCORE_URLS` and `AllowedHosts`. A hosted deployment supplies the exact
host list, such as `<public-host>;localhost;127.0.0.1`; the local default is restrictive.
Runtime/framework environment selection occurs during host
construction. Arbitrary ambient variables are removed from application configuration.

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

## SDK adoption boundary

Published `WoW2.Sdk.Backend.Beta` and its testing package are pinned to `10.0.58-beta`.
NuGet availability was verified on 26 September 2026; source inspection used SDK
HEAD `546466ef67e0b8c5ba1ad01957052c682b2c447f`, with unrelated documentation edits.

The worker calls public SDK `AddHangfireJobs` with explicit PostgreSQL storage
options (`PrepareSchemaIfNecessary=false`). The SDK's PostgreSQL preset auto-creates
schema and its jobs registration always includes a server. Therefore CLI-only
submission uses upstream `AddHangfire` at the host composition boundary. This is
an SDK client-only/schema-control preset gap; no product scheduler abstraction or
private SDK API was introduced. Shared SDK files were not changed.

Application schema migration uses public `AddDatabaseBespokeMigrations(assembly)`
and `AddDataSourceConnectionFactory`. API migration CLI alone initializes Hangfire
storage with schema preparation enabled. No EF model competes for schema ownership.

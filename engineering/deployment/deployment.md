# Deployment and local runtime

*Last updated: 2026-09-26*

## Artifacts and ownership

`Dockerfile` builds the Vue frontend in a Node stage and publishes the API with that generated
`wwwroot`. `Dockerfile.worker` publishes the independent worker. Both build from
`engineering/codebase/`, require NuGet lockfiles, and run as the image's unprivileged `app` user.

Compose names the local stack `brand-workspace`. PostgreSQL stores its data in the
`brand-workspace_postgres-data` volume. A one-shot `migrate` service runs the API `--migrate` command;
both ordinary hosts wait for its successful completion. Neither ordinary host changes schema.

The checked-in `.env.example` contains a clearly labelled local demonstration password. Use a local
ignored `.env` for any different credentials. The scaffold performs no social-provider requests.

## Start the packaged stack

Run from `engineering/deployment/`:

```bash
docker compose --env-file .env.example build
docker compose --env-file .env.example up
```

`up` stays attached so its logs and shutdown remain owned. In another terminal:

```bash
./check.sh
docker compose --env-file .env.example ps
docker compose --env-file .env.example logs --tail 50 api worker migrate
```

The application is at `http://127.0.0.1:8268`. The local container HTTP endpoint is bound only to
loopback; development HTTPS has separate ports below. `check.sh` validates API readiness, actual
worker heartbeat, root/deep-link SPA responses, and JSON errors for unknown API paths and methods.

Stop this stack without deleting its database:

```bash
docker compose --env-file .env.example stop
```

The task owns only this Compose project. Do not stop other products or remove volumes during routine
verification. Backup restoration and production rollout are separate work; they are not proven by
local container startup.

## Schema and harmless queue diagnostics

Apply migrations explicitly when running hosts outside Compose:

```bash
dotnet run --project BrandWorkspace.Api --no-launch-profile -- --migrate
```

That command runs from the backend directory with `ConnectionStrings__Database` configured. Embedded
SQL migrations use the shared SDK runner and PostgreSQL locking; the explicit command also initializes
the provider-owned Hangfire schema. Re-running it verifies already-applied source integrity.

Client-only diagnostics run from the worker image without starting a worker server:

```bash
docker compose --env-file .env.example run --rm --no-deps worker --enqueue-probe
docker compose --env-file .env.example run --rm --no-deps worker --probe-status JOB_ID
```

Replace `JOB_ID` with the returned identifier. The probe writes only its technical completion receipt.
A queued probe remains pending while the worker is stopped; a replacement worker can complete it from
the existing PostgreSQL state. This is a queue durability check, not a publication or exactly-once proof.

## Health semantics

- `/health/live`: the API process serves requests; it does not check dependencies.
- `/health/ready`: PostgreSQL is reachable and the expected foundation/queue schema is present.
- `/api/system/status`: foundation metadata and latest worker heartbeat (`waiting`, `healthy`, `stale`).
- Worker image health: process liveness only. Read system status for heartbeat freshness.

No user content, tenant records, provider credentials, or business mutations exist in these endpoints.
Add authenticated organization boundaries before introducing that state.

## Ports

| Surface | Local port |
|---|---|
| API development | HTTPS 8264 / HTTP 8265 |
| Vue development | HTTPS 8266 |
| Packaged API and SPA | HTTP 8268, loopback only |
| PostgreSQL | 8270, loopback only |
| Worker | No HTTP listener |

Use the shared [port ledger](../../../../../conventions/deployment/hosting/ports.md) when adding services.
Default Vite uses mkcert; certificate trust stays user-controlled. `npm run dev:http` is an explicit
headless fallback. Vite proxies `/api` to the HTTPS development API.

## CI and publication

`verify.yml` runs frontend checks, locked backend restore, the default API/SPA build, backend tests,
and both image builds. `publish-docker-image.yml` is the standard release marker. A human-published
version release builds `ghcr.io/sulton-max/ventures.brand-workspace` and the corresponding
`ventures.brand-workspace-worker` image, each tagged with the release tag and `latest`.
The main image remains the control plane's standard deployable; operating the separate worker and
PostgreSQL still requires this product's topology. No release or remote deployment was created here.

Before public hosting, explicitly set `AllowedHosts` to the intended host names, for example
`<public-host>;localhost;127.0.0.1`; replace the placeholder with the actual hostname. The default
accepts only local hosts. TLS termination, trusted proxies, credentials, backups, authentication,
and organization isolation need their own deployment/product work before storing real brand data.

Executed verification is recorded in [verification](../planning/verification.md).

# Deployment and local runtime

*Last updated: 2026-09-26*

## Artifacts and ownership

`Dockerfile` builds the Vue frontend in a Node stage and publishes the API with that generated
`wwwroot`. `Dockerfile.worker` publishes the independent worker. Both build from
`engineering/codebase/`, require NuGet lockfiles, and run as the image's unprivileged `app` user.
Both images include `libfontconfig1` for the matching SkiaSharp Linux native provider. The API image
build runs `--verify-media-runtime` as that same user: it decodes a known PNG through the production
validator and verifies its hash without contacting PostgreSQL or starting HTTP. This catches missing
native dependencies and incompatible native/managed library versions before the image is accepted.

Compose names the local stack `brand-workspace`. PostgreSQL stores its data in the
`brand-workspace_postgres-data` volume. A one-shot `migrate` service runs the API `--migrate` command;
both ordinary hosts wait for its successful completion. Neither ordinary host changes schema.
The API's separate `brand-workspace_data-protection-keys` volume preserves protected sessions across
process replacement. Its image directory belongs to the unprivileged runtime user.

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

The public application shell and diagnostics are at `http://127.0.0.1:8268`. Operator access requires
HTTPS; [operator setup](operator-access.md) documents provisioning and the optional packaged TLS listener.
The local container HTTP endpoint is bound only to loopback. `check.sh` validates API readiness, actual
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
the provider-owned Hangfire schema. The access migration includes framework-generated Identity DDL;
Identity does not run a second migration system at startup. Re-running verifies source integrity.

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
- `/health/ready`: PostgreSQL is reachable and the expected application/queue schema is present.
- `/api/system/status`: foundation metadata and latest worker heartbeat (`waiting`, `healthy`, `stale`).
- Worker image health: process liveness only. Read system status for heartbeat freshness.

These endpoints expose only technical diagnostics. Authenticated preparation lives under the
organization API boundary and requires current membership.

## Ports

| Surface | Local port |
|---|---|
| API development | HTTPS 8264 / HTTP 8265 |
| Vue development | HTTPS 8266 |
| Packaged API and SPA | HTTP 8268; optional HTTPS 8269; loopback only |
| PostgreSQL | 8270, loopback only |
| Worker | No HTTP listener |

Use the shared [port ledger](../../../../../conventions/deployment/hosting/ports.md) when adding services.
Default Vite uses mkcert; certificate trust stays user-controlled. `npm run dev:http` is an explicit
diagnostic fallback; secure session cookies require the HTTPS frontend. Vite proxies `/api` to the
HTTPS development API. Browser application and API requests remain same-origin.

## CI and publication

`verify.yml` runs frontend checks, locked backend restore, the default API/SPA build, backend tests,
and both image builds. `publish-docker-image.yml` is the standard release marker. A human-published
version release builds `ghcr.io/sulton-max/ventures.brand-workspace` and the corresponding
`ventures.brand-workspace-worker` image, each tagged with the release tag and `latest`.
The main image remains the control plane's standard deployable; operating the separate worker and
PostgreSQL still requires this product's topology. No release or remote deployment was created here.

Before public hosting, explicitly set `AllowedHosts` to the intended host names, for example
`<public-host>;localhost;127.0.0.1`; replace the placeholder with the actual hostname. The default
accepts only local hosts. The initial topology supports direct TLS. A reverse proxy requires an
explicit trusted-proxy configuration; arbitrary forwarded headers do not change request authority.
Production credentials, key encryption, backups, identity recovery, and operational acceptance remain
separate work before storing real brand data. No real brand account is provisioned by scaffolding.

Executed verification is recorded in [verification](../planning/verification.md).

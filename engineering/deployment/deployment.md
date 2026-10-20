# Deployment and local runtime

*Last updated: 2026-10-20*

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
both ordinary hosts wait for its successful completion. The API also verifies the schema as it starts
(nothing is pending by then); the worker never changes schema.
The API's separate `brand-workspace_data-protection-keys` volume preserves protected sessions across
process replacement. Its image directory belongs to the unprivileged runtime user.

The checked-in `.env.example` contains a clearly labelled local demonstration password. Use a local
ignored `.env` for any different credentials. The scaffold performs no social-provider requests.

## Resume the existing local review

From the repository root on this Mac:

```bash
python3 engineering/deployment/run-review.py
```

Open `https://localhost:8264`. Use `smoke@example.test` and the existing password in
`.local/verification/operator-password.txt`; the launcher never displays or resets it.
It requires Python 3.9+, the existing ignored `.local/verification/compose.env`, local Docker Desktop,
the .NET 10 SDK, pnpm 10, and the exact Node version in `.nvmrc`. An existing matching
NVM installation is selected automatically; no tools or certificate trust are installed.

A healthy existing API returns its URL immediately. Otherwise the launcher builds the
current API and locked frontend, explicitly applies migrations, and starts the API
using the existing .NET development certificate and stable `host-keys` directory.
HTTPS verification must succeed. `compose.env` is read as literal key/value data;
shell substitutions and environment interpolation are never evaluated by the launcher.

Keep the terminal open. Ctrl+C, terminal closure, and termination clean up its owned
processes. PostgreSQL is reused when already running; otherwise an attached Compose
child starts it. Cleanup records the container ID and start instance before stopping it;
a replacement or externally restarted instance is preserved. Database volumes, credentials, and
session keys are preserved. The launcher starts no worker and never provisions users.
If another unready process occupies 8264/8265, stop its owning terminal before retrying.

Run the launcher regression checks from the repository root:

```bash
python3 -B -m unittest discover -s engineering/deployment/tests -p 'test_*.py' -v
```

The 12 checks cover literal configuration, readiness, ownership, and shutdown behavior.
They mock process, network, and signal boundaries, use temporary fixture credentials,
and never start Docker or modify a running review. Live runtime acceptance is separate.

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

The public application shell and diagnostics are at `http://127.0.0.1:8377`. Operator access requires
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

The API applies the schema as it starts, before it listens: embedded SQL migrations through the shared SDK
runner under a PostgreSQL advisory lock, then the provider-owned Hangfire schema. Readiness therefore never
precedes them; an unreachable database is retried ten times, two seconds apart, and any other failure aborts
startup, so a rollout stops on an unmigrated schema. The same steps run explicitly, without serving:

```bash
dotnet run --project BrandWorkspace.Api --no-launch-profile -- --migrate
```

That command runs from the backend directory with `ConnectionStrings__Database` configured.
The access migration includes framework-generated Identity DDL; Identity does not run a second
migration system. Re-running verifies source integrity.

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
| API development | HTTPS 8264; 8265 reserved |
| Vue development | HTTPS 8378 |
| Packaged API and SPA | HTTP 8377; optional HTTPS 8376; loopback only |
| PostgreSQL | 8270, loopback only |
| Worker | No HTTP listener |

Use the shared [port ledger](../../../../../../../../../conventions/deployment/hosting/ports.md) when adding services.
Default Vite uses mkcert; certificate trust stays user-controlled. Development and browser checks use HTTPS only. Vite proxies `/api` to the
HTTPS development API. Browser application and API requests remain same-origin.

## CI and publication

`publish-docker-image.yml` is the Wheelhouse marker and the only CI file: it calls the shared `delivery.yml` of
`wow-two-platform.pipelines`, pinned to `v0.9.1`, with the checks below. A docs-only push builds nothing; any
other runs the checks whose paths changed (each app's frontend, backend and pilot image; the image worker's tests;
the deployment composition) and every check when a shared file changes. Publication then builds the services in
`deploy.yml` that changed, as `ghcr.io/ocharo-hq/ocharo-platform/{desk,jobs,image-worker}`; CI builds only the
Brand and Catalogue pilot images, which never publish. Jobs fetch only the LFS files under `engineering/codebase/`. Work happens on `main` and short-lived
branches only: a `main` build releases `vX.Y.Z` with `ocharo-platform-release.tar.gz` and reaches dev
automatically; any other branch builds a candidate that reaches dev only by hand.
Wheelhouse deploys that bundle; PostgreSQL, settings and the model volume come from its targets.

## Target settings

Pipelines `v0.7.0` mounts each service's target settings file at `/app/appsettings.Target.json`; the
backend SDK (`10.0.71-beta`) reads it. Both environments run as .NET `Production`: `deploy.yml` leaves
`dotnetEnvironment` off. The runner refuses a deploy missing a required key:

| Service | Required settings |
|---|---|
| `desk` | `ConnectionStrings:Database`, `AllowedHosts`, `DataProtection:KeysPath`, `DataProtection:ApplicationName`, `Deployment:TrustedProxies` |
| `jobs` | `ConnectionStrings:Database` |
| `image-worker` | `ImageWorker:Key` (2 GB memory) |

- `AllowedHosts` lists the public host and `localhost` for health probes, for example `desk.example.com;localhost`.
- `DataProtection:KeysPath` is the `keys` volume, `/var/lib/brand-workspace/keys`.
- `Deployment:TrustedProxies` is a JSON array naming the ingress, `["172.30.0.2"]` on the shared VPS's
  `platform` network. Forwarded headers from any other sender are ignored, so without it sign-in fails
  behind the TLS-terminating ingress.
- `jobs` declares `dependsOn: [desk]`: it refuses to start before the schema exists, and Desk reports
  healthy only after migrating.

`desk` also reads optional public settings, which it serves anonymously at `GET /api/runtime-config` for the
Ocharo app menu. Each is an absolute HTTPS address without credentials, query or fragment:

| Setting | Runtime-config key | Links to |
|---|---|---|
| `OcharoApps:Desk` | `OCHARO_DESK_URL` | Desk |
| `OcharoApps:Catalogue` | `OCHARO_CATALOGUE_URL` | Catalogue |
| `OcharoApps:Studio` | `OCHARO_STUDIO_URL` | Studio |
| `OcharoApps:Marketing` | `OCHARO_MARKETING_URL` | Marketing |
| `OcharoApps:Editorial` | `OCHARO_EDITORIAL_URL` | Editorial (the Blog studio) |
| `OcharoApps:Brand` | `OCHARO_BRAND_URL` | Brand |
| `OcharoApps:Atlas` | `OCHARO_ATLAS_URL` | Atlas public site |
| `OcharoApps:Wear` | `OCHARO_WEAR_URL` | Wear public site |
| `OcharoApps:Blog` | `OCHARO_BLOG_URL` | Blog public site |

- Dev and prod values are each environment's tailnet site URL, for example `https://ocharo-desk-dev.<tailnet>.ts.net/`
  for `OcharoApps:Desk` on dev.
- A key left unset keeps the build-time `VITE_OCHARO_*` value, else the local-run `localhost` address, so set every
  key the menu should link to on a deployed target.
- Desk leaves out a malformed value and logs a startup warning naming the setting; the menu omits that app.
- A changed address takes effect on the next page load; no rebuild or redeploy of the image is needed.

Production credentials, key encryption, backups, identity recovery, and operational acceptance remain
separate work before storing real brand data. No real brand account is provisioned by scaffolding.

Executed verification is recorded in [verification](../research/previous-work/verification.md).

## Convention adoption status

`deploy.yml` lists Desk, jobs and the private image worker; the Brand and Catalogue pilots have no descriptor.
Desk probes `/health/ready`, its own readiness route, until it adopts the SDK boot bundle's `/health`.

See `../research/convention-audit.md` for the earlier verified checks.

# Operator access and preparation

*Last updated: 2026-09-26*

## Account ownership

This increment uses local operator accounts managed by ASP.NET Core Identity. Accounts are created
through the API executable's administrator command. There is no public registration or bootstrap
endpoint. Provisioning an operator does not grant access to an existing organization. Creating a new
organization grants its creator an owner membership in the same transaction.

Build the packaged images and start PostgreSQL using [deployment commands](deployment.md) before
provisioning. The following commands use `--no-deps`, so they require an already healthy database.
Run schema migrations before provisioning, from this directory:

```bash
docker compose --env-file .env run --rm --no-deps migrate --migrate
docker compose --env-file .env run --rm --no-deps migrate \
  --provision-operator --email operator@example.com --name 'Workspace operator'
```

Use the same ignored `.env` and database credentials for startup, migration, provisioning, and HTTPS.
For the demonstration stack, `.env.example` can consistently replace `.env` until TLS settings are
added to an ignored file. Changing `POSTGRES_PASSWORD` does not rotate the database password already
stored in a PostgreSQL volume; preserve that existing password when adding certificate settings.

Replace the example identity with the intended operator. The command reads the password from the
terminal or standard input, never a password argument. Existing accounts are rejected; this is not a
password-reset command. Apply a strong unique password satisfying the displayed Identity validation.

For a host development runtime, the equivalent command runs from the backend directory, with
`ConnectionStrings__Database` configured for the local database:

```bash
dotnet run --project BrandWorkspace.Api --no-launch-profile -- \
  --provision-operator --email operator@example.com --name 'Workspace operator'
```

The normal HTTP host requires `DataProtection__KeysPath` outside Development. Compose supplies this
automatically. A local host can use a private absolute directory under the ignored `.local/` folder.
Protect its contents: possession of Data Protection keys is security-sensitive. Migration and operator
provisioning require PostgreSQL but no HTTP listener, HTTPS certificate, or key ring. The independent
`--verify-media-runtime` command decodes a known image without PostgreSQL or HTTP configuration.

## HTTPS and sessions

Authentication requires HTTPS. The ordinary Compose HTTP listener at port `8377` supports diagnostics
and the public application shell; it cannot sign an operator in. Host development uses the configured
HTTPS profile at `https://localhost:8264`, with an existing locally trusted .NET development certificate.
Certificate creation or trust changes remain explicit local setup actions.

The optional `docker-compose.https.yml` starts a direct Kestrel TLS listener at
`https://localhost:8376`. Supply the absolute path and password of your local PFX certificate through
the ignored deployment `.env`:

```dotenv
POSTGRES_PASSWORD=use-the-existing-local-database-password
BRAND_WORKSPACE_HTTPS_CERTIFICATE=/absolute/private/path/localhost.pfx
BRAND_WORKSPACE_HTTPS_PASSWORD=replace-with-local-certificate-password
```

Run with both Compose files:

```bash
docker compose --env-file .env -f docker-compose.yml -f docker-compose.https.yml up
```

The certificate is mounted read-only into the API. It is never an image input. Authentication uses a
Secure, HttpOnly cookie, an eight-hour absolute session, live session validation, and session-bound
antiforgery tokens. Logout revokes the server session. Organization authority is resolved from live
membership, not a selected client ID. Identity provider changes can preserve these memberships.

The named `brand-workspace_data-protection-keys` volume preserves cookie protection across API
replacement. The application discriminator is `brand-workspace:Production` for the packaged stack.
Keys must not be shared between unrelated environments. This local volume is not encrypted by the
application; production key encryption, backups, TLS certificate rotation, and host permissions remain
deployment decisions before storing real brand data.

## Preparation flow

1. Sign in with a provisioned operator.
2. Create or select an organization, then create a brand with an explicit timezone.
3. Upload real PNG/JPEG source media with provenance.
4. Save a draft with copy, language, destination, source notes, and selected media.
5. Request review of the saved revision. An owner can approve that exact revision.
6. Prepare and download a manual ZIP containing the caption, manifest, and original selected media.

Editing creates a new revision and clears current review/approval. A stale editor must resolve the
version conflict before saving. Owner self-review is currently allowed and recorded. Editors can
prepare content; viewers can read. Membership administration is not exposed in this first interface.

An export is a prepared snapshot, not a publication receipt. Approval revocation prevents new packages
but cannot recall existing prepared copies. Previously created packages stay immutable and downloadable
by current authorized organization members. No social provider receives content from this workflow.

## Packaged verification

`check-preparation.py` uses Python's standard library and validates the HTTPS certificate. It accepts
only a loopback HTTPS origin and an explicitly provisioned `example.test` operator. The script creates
synthetic organizations, a one-pixel image, a draft, and an export; it leaves those records for inspection.
Use a dedicated local verification database for repeatable clean runs.

```bash
python3 check-preparation.py --origin https://localhost:8376 --email smoke@example.test
```

The password comes from a masked terminal prompt or standard input. For a dedicated test certificate,
use `--ca-file /absolute/path/test-certificate.pem`; chain and hostname verification stay enabled.
The check verifies sign-in, antiforgery, organization isolation, immutable image bytes, stale-save
conflicts, review, approval, ZIP contents, idempotency, revocation, and logout. Its successful result
does not establish real-user acceptance, provider access, or production readiness.

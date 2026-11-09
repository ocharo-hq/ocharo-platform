# Operator access and preparation

*Last updated: 2026-11-02*

## Account ownership

Operators sign in through Ocharo's own WoW2 Identity instance; Desk keeps no passwords and offers no sign-in of its own
(cutover 2026-11-02, [identity cutover](../research/identity-cutover.md)). A person joins Ocharo through an invite an
owner or admin makes at Identity; the invite's role reaches Desk as `org_role` and its permissions as `org_permission`
(`desk:edit`, `desk:approve`). Desk reads its client from the `Identity:Client` section (`Authority`, `ClientId`,
`ClientSecret`, `CookieName`); a local run keeps them in the ignored `BrandWorkspace.Api/appsettings.Local.json`.

Run schema migrations from this directory before starting the stack:

```bash
docker compose --env-file .env run --rm --no-deps migrate --migrate
```

The normal HTTP host requires `DataProtection__KeysPath` outside Development. Compose supplies this
automatically. A local host can use a private absolute directory under the ignored `.local/` folder.
Protect its contents: possession of Data Protection keys is security-sensitive. Migration requires
PostgreSQL but no HTTP listener, HTTPS certificate, or key ring. The independent
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

The certificate is mounted read-only into the API. It is never an image input. Sign-in runs the OpenID Connect code
flow against Identity through the SDK's BFF: a Secure, HttpOnly cookie carries only a session reference, the tokens stay
server-side, unsafe API requests carry the session-bound `X-Csrf-Token`, and the session ends with the Identity sign-in
session. Organization authority comes from the session's `org_id`, `org_role` and `org_permission` claims; Desk projects
each signed-in person into `access.users`, and the token's organization into `access.organizations`, on
their first organization request; a hosted Desk with no restored data needs no seed. The row takes its name and
slug from the token's `org_name` and `org_slug`; a slug another organization holds falls back to `org-{id}`.

The named `brand-workspace_data-protection-keys` volume preserves cookie protection across API
replacement. The application discriminator is `brand-workspace:Production` for the packaged stack.
Keys must not be shared between unrelated environments. This local volume is not encrypted by the
application; production key encryption, backups, TLS certificate rotation, and host permissions remain
deployment decisions before storing real brand data.

## Preparation flow

1. Sign in by phone through Identity; the code arrives in Telegram.
2. Open the organization, then create a brand with an explicit timezone.
3. Upload real PNG/JPEG source media with provenance.
4. Save a draft with copy, language, destination, source notes, and selected media.
5. Request review of the saved revision. An owner can approve that exact revision.
6. Prepare and download a manual ZIP containing the caption, manifest, and original selected media.

Editing creates a new revision and clears current review/approval. A stale editor must resolve the
version conflict before saving. Owner self-review is currently allowed and recorded. Editors can
prepare content; viewers can read. Members and invitations are managed at Identity.

An export is a prepared snapshot, not a publication receipt. Approval revocation prevents new packages
but cannot recall existing prepared copies. Previously created packages stay immutable and downloadable
by current authorized organization members. No social provider receives content from this workflow.

## Packaged verification

`check-preparation.py` signed in with an operator password and retired with Desk's own sign-in on 2026-11-02. The E2E
suite (`tests/BrandWorkspace.Tests.E2E`) covers sign-in boundaries, organization isolation, review, approval and export
with test principals; a browser sign-in through Identity covers the rest.

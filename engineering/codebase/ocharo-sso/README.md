# Ocharo application sign-in

Implemented and locally tested; disabled until operators supply the registrations below. No live credentials, provider settings or running services are changed by this implementation.

## Architecture

Ocharo Desk owns accounts, organization memberships and revocable eight-hour sessions. Telegram OTP and password login issue that same Desk session. Each operator app starts a browser round trip to Desk. An existing Desk login authorizes the app without another credential prompt.

This is a bounded internal Ocharo delegation protocol, not an OAuth or OpenID Connect provider. Use a standards-based provider when opening authorization to third-party clients.

1. App backend creates random state, correlation cookie and S256 verifier; browser goes to the fixed Desk authorization URL.
2. Desk validates an explicit client, organization, exact HTTPS callback and current membership, then issues a 60-second single-use code.
3. App backend redeems the code with its secret and verifier. Desk atomically consumes the code and stores only a hash of a rotating refresh grant.
4. Browser receives only a random app session cookie (`__Host-ocharo-{app}`); grant and ticket stay in the backend. App cookies are distinct even on localhost ports. There is no shared domain cookie or shared DataProtection key ring.
5. Every private request introspects its ticket against the current Desk session and membership. Ticket refresh is serialized per app session, rotates atomically and cannot extend the original Desk session expiry. Network failures deny access.

Client cookies are Secure, HttpOnly, SameSite=Lax, Path=/ with no Domain. Browser writes require a separate CSRF token and origin checks. Backend exchanges reject browser Origin/Sec-Fetch-Site headers and require client authentication; only these explicit backchannels skip Desk antiforgery checks. App logout revokes that app grant; Desk logout revokes the central session and all delegated access on the next request.

## Trust configuration

Desk has `Sso:Enabled` (default false) and `Sso:Clients`, with entries:

| Field | Requirement |
|---|---|
| Id | Unique ASCII letters/digits/hyphen, 1–80 characters |
| RedirectUri | Exact HTTPS app origin plus `/api/sso/callback`; no query, fragment or userinfo |
| Audience | `ocharo-catalogue`, `ocharo-brand`, `ocharo-studio`, `ocharo-marketing`, `ocharo-editorial`, or `ocharo-assets` |
| OrganizationId | Existing explicit nonempty Desk organization UUID |
| SecretSha256 | Hex SHA-256 digest of that app's random secret (raw secret 32–512 characters) |

Desk also needs its existing `Integrations:SigningKey` (at least 32 UTF-8 bytes). Enabled malformed/duplicate registrations fail startup. Secrets belong in secret configuration, never source, logs or frontend environment variables. Apply migration `010-app-sso` through the existing migration runner before enabling the broker.

Each app uses `Identity:Sso` with `Enabled=false`, `DeskOrigin`, `ClientOrigin`, `ClientId`, `ClientSecret`, and `OrganizationId`. Both origins must be fixed HTTPS origins, without paths. Catalogue, Brand, Marketing and Editorial require an explicit organization binding because their current local databases are not multi-tenant. Studio keeps its existing organization+member workspace partition. The Desk client registration still binds Studio to an explicit organization.

Do not enable production solely because the code builds. Configure HTTPS/reverse proxy trust, client origins, organization membership, secrets and a durable multi-instance app-session store before a multi-instance rollout. Current app-session storage is bounded, single-process memory; restart invalidates app cookies and Desk can authorize again. Marketing's existing local-pilot network restriction remains in force.

## HTTP contracts

- `GET /api/sso/authorize?clientId&redirectUri&codeChallenge&state` on Desk. Anonymous users resume via `/?returnUrl=<local authorize URL>` after Desk login.
- `POST /api/sso/exchange` on Desk: clientId, clientSecret, redirectUri, code, codeVerifier.
- `POST /api/sso/refresh` and `/api/sso/revoke` on Desk: clientId, clientSecret, refreshToken.
- Successful exchange/refresh: `{ data: { ticket, expiresAt, refreshToken, sessionExpiresAt } }`; revoke: 204. These are backend-only responses.
- `POST /api/integrations/introspect` on Desk: token, audience. Active: `{ data: { active, userId, organizationId, role, canEdit, canApprove, expiresAt, displayName, email } }`; otherwise `{ data: { active: false } }`. `displayName` is the operator's current Desk profile name, or their email when the name is blank; either value is null when absent. The response is no-store.
- `GET /api/sso/login?returnUrl=/...` on an app starts sign-in. Local returns must begin one slash, be at most 2048 characters and exclude `%`, backslash, control characters and `/api/` paths.
- `GET /api/sso/session` on an app: `{ data: { enabled, authenticated, accountUrl?, userId?, organizationId?, name?, email?, canEdit?, canApprove?, csrfToken? } }`. The response is no-store.
- `name` and `email` come from that request's live Desk introspection (`SsoActor.DisplayName`/`Email`), so a profile change shows on the next request. They are present only when authenticated, and are null when an older Desk omits them or a value is blank, longer than 200 (name) or 256 (email) characters, or contains control characters; a missing value never fails the session. Anonymous and disabled responses carry neither key. Neither value is written to the app cookie or the app session store: the cookie stays a 43-character random id and the store keeps only the grant.
- App browser mutations attach `X-CSRF-Token` with same-origin credentials. `POST /api/sso/logout` uses the same protection.

Desk SSO bodies are limited to 16 KiB. Authorization/exchange share 30 attempts/minute per remote IP; refresh/revoke share 120. Rejections are ProblemDetails with no-store; rate rejection includes Retry-After. Configure trusted forwarding at the host before using forwarded IPs. Callback/exchange secrets are never request-log fields.

Public catalogue readers and published journal/media remain anonymous. Private Editorial draft media and previews still pass through their existing content-aware boundary. Marketing approval operations retain their extra approver requirement. Legacy ticket connection is only available when SSO is disabled.

## Distribution and verification

`Ocharo.Sso` is the authoritative local component. Catalogue/Brand reference it within their platform repository. Studio/Marketing/Editorial contain byte-identical generated source snapshots with SHA-256 manifests, so standalone CI needs neither another checkout nor an unpublished package. Their `shared/sync-sso.py --check` rejects drift; `--source <authoritative Ocharo.Sso directory>` also verifies source equality; `--update --source ...` refreshes a snapshot. A separately reviewed SDK release should replace these snapshots.

Verified locally: shared HTTP security tests; Desk actual HTTP/PostgreSQL code binding, replay/concurrency, session/membership revocation, disabled mode and configuration/request limits; Studio workspace isolation; Marketing/Editorial role/org boundaries; public journal and draft-media protection. Tests use isolated credentials and temporary stores. This evidence does not claim live browser sign-in or production activation.

Expired code/grant rows contain only digests; schedule retention cleanup as an operational follow-up before sustained production traffic. Do not delete active grants during cleanup. Their expiry is enforced independently of cleanup.

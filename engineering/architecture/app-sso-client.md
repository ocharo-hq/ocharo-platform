# Ocharo application SSO clients

## Local implementation

Desk remains the account authority. Each application redirects through Desk using a fixed registered callback and S256 PKCE, then keeps its own host-only, Secure, HttpOnly cookie. Signing in once to Desk lets another application complete that redirect without asking for credentials again.

The authoritative local component is `engineering/codebase/ocharo-sso/Ocharo.Sso`. Catalogue and Brand reference it within this repository. Studio and Marketing use generated exact source snapshots with SHA-256 provenance until a reviewed SDK/package release replaces them. No unpublished NuGet dependency or cross-repository project reference is required by standalone CI.

Enablement is explicit; default `Identity:Sso:Enabled` is false. The existing local pilot remains the disabled path. Once enabled, private APIs deny anonymous callers and use live Desk introspection on every request. Catalogue and Brand require a single configured organization because their current data stores are not partitioned by organization.

## Server configuration

`Identity:Sso` contains:

- `Enabled`: false until every client and matching Desk registration is verified.
- `DeskOrigin`: fixed HTTPS account-authority origin.
- `ClientOrigin`: fixed HTTPS application origin.
- `ClientId`: registered identifier containing letters, digits and hyphens.
- `ClientSecret`: server-only random secret, at least 32 characters. Desk stores its SHA-256 hash.
- `OrganizationId`: mandatory fixed organization for Catalogue and Brand; optional additional binding for partitioned clients.

Register the exact `{ClientOrigin}/api/sso/callback` in Desk. Do not derive callbacks from `Host`, `Origin`, query parameters or user-entered destinations. No wildcard hosts, parent-domain cookies, browser refresh tokens or shared application signing keys are used.

## Lifecycle and interface

- `GET /api/sso/login?returnUrl=/local/path` starts authorization; destinations are bounded local application paths.
- `GET /api/sso/callback` requires the initiating browser's correlation cookie and single-use state.
- `GET /api/sso/session` returns enabled/authenticated state, capabilities, a session CSRF token and, when authenticated, the operator's current Desk `name` and `email` (null when Desk has none); no delegated or refresh credentials.
- Cookie-based writes require `X-CSRF-Token` and a compatible origin. Bearer integrations still require live, audience-bound authorization.
- `POST /api/sso/logout` ends this application's grant only. It does **not** end the Desk session. Use Desk's account controls for global sign-out. App navigation may transparently authorize again while Desk remains signed in; no UI claims otherwise.
- A failed write is never replayed. On session expiry, Catalogue, Brand, Marketing and Editorial reconnect in another tab and preserve unsaved editor content; the user explicitly checks the restored connection before saving again. Studio uses same-tab authorization only after its dirty/saving guard permits navigation, with its existing recovery mechanism preserving other drafts.
- Read-only and publication capabilities come from Desk; server checks remain authoritative.

The initial grant store is bounded and in memory, with per-session serialization for rotating refresh grants. It supports one application instance. Restarting an application discards its grants and fails closed; Desk can restore access through a fresh authorization redirect. Multi-instance deployment needs a durable distributed store with atomic refresh ownership before enablement.

Backchannel calls use five-second budgets, disabled automatic redirects/cookies, suppressed request logging, and a 32 KiB streamed response limit. Browser callback responses use `no-store` and `no-referrer`. Catalogue public media must belong to a live eligible product or a published, unwithdrawn release whose design remains available; current and pinned historical releases share this authority. Private media requires live app authorization and uses `private, no-store`; public media uses `public, no-cache` so changed visibility is rechecked.

## Evidence

Shared component: 21 HTTP tests pass against distinct synthetic Desk/app origins, covering PKCE, correlation, callback replay, cookies, CSRF, live revocation, organization and write permissions, concurrent refresh, logout, optional media authority, disabled mode, oversized responses, restart invalidation and session identity (current name/email, legacy Desk responses without them, unsafe values and anonymous omission).

Catalogue: 59 tests pass. Brand: 10 tests pass. Each suite includes enabled-host authorization checks alongside retained pilot behavior, including case-insensitive publication routes with trailing slashes. Catalogue additionally tests anonymous media eligibility, private no-store reads, prepared-only exclusion and historical release withdrawal/archive. The final combined Desk Telegram/password/SSO, integration-ticket and media-authorization gate passes 69 tests against disposable databases and fake Telegram transport. These results do not establish live provider activation or deployment.

Catalogue frontend: 169 tests pass. Brand frontend: 22 tests pass. Both typechecks, lint and production builds passed using Node 24. Existing Catalogue lint warnings are confined to multiple test components; generated bundle size warnings remain unchanged.

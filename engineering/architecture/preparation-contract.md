# Preparation foundation contract

*Last updated: 2026-09-26*

## Implemented increment

Continue the authorized scaffold into a real preparation workflow: local operator sign-in → organization
membership → brand → immutable uploaded media → versioned draft → review → approval → manual ZIP.
Use actual PostgreSQL state and protected cookies. No social account or publishing provider is connected.
Local operators are explicitly provisioned through an administrator CLI; public registration and
email delivery are not part of this increment. Ocharo is user-entered organization/brand data.

This remains one coordinated product bootstrap. Access and preparation are dependent parts of the
same workflow; independent future subsystems require their own vector ownership map.

## Access and security

Use ASP.NET Core Identity for password hashing, password verification, and lockout, composed with the
published wow-two cookie/default-deny primitives. Keep identity storage in PostgreSQL. Generate the
Identity schema from its EF mapping if using EF stores, then apply it as an embedded numbered SQL
migration through the existing sole `--migrate` owner. Normal startup never applies schema.

Persist revocable sessions. A protected Secure/HttpOnly/SameSite cookie carries the authenticated user
and session identifier; validate active session/account state on every request. Use an eight-hour
absolute lifetime. Persist Data Protection keys with a product/environment discriminator.

Framework antiforgery protects every unsafe API request, including login/logout. Bind request tokens
to the session identifier through `IAntiforgeryAdditionalDataProvider`, not just the user name. GET session
issues a request token; the UI sends `X-CSRF-TOKEN`. Require HTTPS for authentication; do not add an
insecure development sign-in bypass. Test-only principals remain in test assemblies.

Organization route filters resolve membership from the authenticated user on every request. Body/header
organization or actor IDs never confer authority. Every preparation query includes organization scope;
composite foreign keys prevent cross-organization associations. Missing/foreign resources return 404.
Roles: owner can approve and administer; editor can prepare; viewer can read. Membership changes are
not exposed in this initial UI, but removing a row must immediately revoke its authority.

CLI contract: `--provision-operator --email <email> --name <display-name>` reads a strong password from
standard input without logging it. It creates only a missing operator, rejects existing accounts, and
never creates an implicit organization or owner membership. Tests use reserved example.test addresses.

## HTTP conventions

Success JSON is `{ "data": T }`; errors are ProblemDetails, with `code` where useful. System diagnostics
retain their existing bare JSON contracts. All user/workspace responses use Cache-Control:no-store.
Mutating inputs are bounded and validated; backend conflicts return 409 and preserve the old state.
Identifiers are UUID strings except framework Identity user IDs (opaque strings).

| Endpoint | Request | Data response |
|---|---|---|
| GET /api/auth/session | none | Session |
| POST /api/auth/login | email,password | authenticated:true |
| POST /api/auth/logout | empty JSON | HTTP204 |
| GET /api/organizations | none | Organization[] |
| POST /api/organizations | name,slug | Organization (current actor becomes owner atomically) |
| GET /api/organizations/{organizationId}/brands | none | Brand[] |
| POST /api/organizations/{organizationId}/brands | name,timeZone | Brand |
| GET /api/organizations/{organizationId}/media | none | MediaAsset[] (bytes excluded) |
| POST /api/organizations/{organizationId}/media | multipart:file,provenance | MediaAsset |
| GET /api/organizations/{organizationId}/media/{mediaId}/content | none | authenticated image bytes, nosniff |
| GET /api/organizations/{organizationId}/drafts | optional brandId query | DraftSummary[] |
| POST /api/organizations/{organizationId}/drafts | DraftInput | Draft |
| GET /api/organizations/{organizationId}/drafts/{draftId} | none | Draft |
| PUT /api/organizations/{organizationId}/drafts/{draftId} | expectedRevision + DraftInput | Draft |
| POST /api/organizations/{organizationId}/drafts/{draftId}/request-review | expectedRevision | Draft |
| POST /api/organizations/{organizationId}/drafts/{draftId}/approve | expectedRevision | Draft |
| POST /api/organizations/{organizationId}/drafts/{draftId}/revoke-approval | expectedRevision,reason | Draft |
| POST /api/organizations/{organizationId}/drafts/{draftId}/exports | expectedRevision,idempotencyKey | ManualExport |
| GET /api/organizations/{organizationId}/exports/{exportId}/download | none | authenticated ZIP snapshot |

Session: `{ authenticated, user: {id,email,displayName} | null, sessionId: string | null,
csrfToken, setupRequired }`. Setup-required means no operator exists, not a public bootstrap grant.
Organization: `{id,name,slug,role}`. Brand: `{id,organizationId,name,timeZone}`.
MediaAsset: `{id,organizationId,fileName,contentType,byteLength,sha256,provenance,createdAtUtc}`.
DraftInput: `{brandId,title,caption,language,destination,sourceNotes,mediaIds: string[]}`.
DraftSummary: `{id,brandId,title,revision,status,updatedAtUtc}`.
Draft: DraftSummary + `{caption,language,destination,sourceNotes,media:MediaAsset[],
requestedReviewRevision:number|null,approval:{id,revision,approvedAtUtc,approvedBy}|null}`.
Status: `draft` | `in-review` | `approved`; always describes the current head revision.
ManualExport: `{id,draftId,revision,createdAtUtc,downloadUrl,packageKind:"manual-content-package",
mediaIncluded:true,publicationStatus:"unverified"}`. ZIP always includes a manifest and caption.txt;
mediaIncluded indicates the package supports/inlines its selected immutable media (zero selections
remain an explicit empty array in the manifest).

## Preparation invariants

Brands require a valid IANA timezone. This is configuration, not scheduling support.
Accept PNG/JPEG only, at most 5 MiB per asset, 10 assets per draft, 25 MiB combined export media.
Validate actual image structure using a supported decoder/header reader, cap dimensions/pixel count,
and reject SVG, malformed data, traversal filenames, empty input, and oversized requests. Hash actual
bytes; keep the original byte array immutable in PostgreSQL for this bounded first storage adapter.
This adapter can later move to object storage without changing media identity/provenance contracts.
Never fetch user-provided URLs or read arbitrary filesystem paths.

Draft revisions are immutable; the draft row is its mutable head. A successful save requires
expectedRevision, inserts one new revision, and atomically advances the head. Any edit clears the
current review/approval state without destroying historical approvals. Approval binds exact caption,
language, destination, source notes, ordered media IDs/hashes, and revision. Approval requires a review
request for that same revision. Owner role may self-review in this single-operator initial workflow;
the recorded actor makes that visible, and a two-person rule is a later configurable policy.

Save, request-review, approve, revoke, and export serialize on the same draft row. Export checks the
current active approval and copies its exact immutable snapshot. Store an organization-scoped
idempotency key and request hash: retries return the same export; a reused key with different input
conflicts. Revocation prevents new exports; an already prepared copy cannot be recalled.
ZIP entry names are generated from immutable IDs, never raw user paths. Download rechecks membership
and export organization. Generating/requesting a package does not mean it was downloaded or published.

## Frontend composition

Use published SDK auth/forms/query/layout/HTTP APIs. Its FormData request support is verified; use
multipart upload. Download through the returned same-origin authenticated URL, avoiding an invented
binary HTTP adapter. Preserve edited values when a save conflicts or the service fails.

Sign-in, logout, and organization switching use full-document navigation so each document gets its
own client/cache. Handle bfcache restoration and cross-tab auth changes; old session-bound antiforgery
tokens must fail. Confirm before leaving unsaved work and avoid context switching during pending writes.
Protected API authority always remains on the server. Keep foundation diagnostics secondary.

## Coordination and reserved ownership

- Access implementation owns all backend Access namespaces, migration `002-access`, identity/session
  tests, central package/solution changes, and shared host wiring. It exposes
  `BrandWorkspace.Application.Access.OrganizationAccess` (OrganizationId,UserId,Role,CanEdit,CanApprove)
  and `BrandWorkspace.Api.Access.OrganizationAccessFilter.GetAccess(HttpContext)`; the filter resolves
  live membership and puts trusted context on organization route groups.
- Preparation implementation owns backend Preparation namespaces, migration `003-preparation`, and
  preparation tests. It exposes `AddPreparationServices(IServiceCollection)` and
  `MapPreparationEndpoints(WebApplication)` for the host owner to call.
- Frontend implementation owns the frontend tree only.
- Root owns deployment, root/project docs, runtime verification, and commits. No agent commits alone.

Only the migration owner edits existing migration history. Both new migrations belong to the pending
rough increment, and must remain ordered after `001-foundation`. No shared SDK source is modified
without a separately coordinated gap fix; source-only APIs are not assumed published.

# Preparation SDK audit

*Last updated: 2026-09-26*

## Adopted package boundary

This increment targets published backend `WoW2.Sdk.Backend.Beta` `10.0.58-beta` and frontend
`@wow-two-beta/ui-vue` `0.0.6`. The sibling SDK checkouts contain newer source than these releases;
source presence alone does not establish a usable consumer API. The first preparation implementation
stays inside this product; extraction follows a completed rough increment and a dedicated capability pass.

## Backend findings

| Capability | Verified published boundary | Product implication |
|---|---|---|
| Protected cookies | `AddCookieAuthentication` with Secure/HttpOnly defaults and API challenges | Compose with live durable session validation and explicit expiration |
| Default-deny authorization | `AddDefaultDenyAuthorization` | Mark only the intended shell, health, session, and login surfaces anonymous |
| User accounts | Core account CRUD and security-stamp primitives, not a complete sign-in engine | Use ASP.NET Core Identity for password validation, hashing, and lockout |
| Tenant resolution | SDK claim/header/route resolution is a context capability | Resolve live organization membership before granting product authority |
| Tenant persistence helpers | Null tenant context does not itself deny all access | Use explicit organization predicates and composite foreign keys |
| Data sessions | `IDataSession`/`AddDataSession` exist in newer source, absent from the pinned package | Keep explicit Npgsql transaction boundaries; do not reference unpublished APIs |
| SQL migrations | Embedded SQL runner with PostgreSQL locking | Keep `--migrate` as the sole schema owner, including generated Identity DDL |

Image decoding uses explicit matching `SkiaSharp` and `SkiaSharp.NativeAssets.Linux` `4.152.1` pins.
The SDK's transitive Linux `3.119.2` native package cannot coexist with a different major decoder through
the separate `Linux.NoDependencies` package: both supply `libSkiaSharp.so`, and the older binary can win
at publish time. One normal Linux provider supplies the runtime asset; both images include its
`libfontconfig1` dependency. Product verification covers the decoder API used here; it does not claim
that every unused SDK media feature has been validated against the explicit dependency override.

The existing SDK `AddUserAccounts` registration does not establish all sign-in, lockout, CSRF, or
revocable-cookie requirements. The Identity API helper is a registration wrapper; it is not evidence
that unrestricted public endpoint mapping is appropriate for operator provisioning. The preparation
contract therefore states the required engine and authority boundaries explicitly.

## Frontend findings

| Capability | Verified published boundary | Product implication |
|---|---|---|
| HTTP multipart | `FormData` identity preserved; browser chooses the multipart boundary | Upload real source bytes through the public HTTP adapter |
| Request context | Credentials and per-request headers supported | Send same-origin cookies and the current antiforgery token |
| JSON requests | Public result/JSON decoding available | Keep application DTOs in the product integration layer |
| Binary responses | No general blob response contract in this pin | POST an export snapshot, then use its authenticated same-origin download URL |
| Query lifetime | `clear()` is published; newer request-generation/disposal APIs are not | Recreate document/runtime on principal and organization changes |
| Forms and shell | Public auth/forms/layout components are available | Compose these into product-specific preparation flows |

Full-document transitions still require handling browser history restoration and cross-tab sign-in
changes. Server membership checks and session-bound antiforgery remain authoritative even if an old
document survives in another tab. Draft conflicts must not erase the operator's unsaved values.

## Shared capability candidates

The product can prove needs before library extraction. Candidate vectors include operator provisioning
and revocable sessions, organization membership authorization, immutable source/media storage, versioned
review and approval, manual artifact packaging, and principal-scoped frontend request lifecycles.

Infrastructure and general brand contracts should remain reusable. Ocharo identity, apparel semantics,
copy, source records, provider authorization, and actual media stay outside the shared libraries.
Extracting a vector means inventorying its essential capabilities, public contracts, tests, and adoption;
moving one product helper into a library is not vector completion.

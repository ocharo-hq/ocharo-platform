# Verification

*Last updated: 2026-09-26*

## Scope and source

The developer's documentation baseline is `04a13f5952e15d3ee862647fe0f8757a42046416`.
The adapted template foundation is `d4b1add`. This record covers the following authenticated
preparation increment under [r0.2](rough-track/r0.2/r0.2.md). The initial history and remote are
preserved. No remote push, release, provider connection, or publication was performed.

The product uses published backend SDK `10.0.58-beta`, Vue SDK `0.0.6`, eight NuGet lockfiles,
and `package-lock.json`. Verification used .NET SDK `10.0.300`, Node `24.11.0`, npm `11.6.1`,
Docker `29.8.0`, and Compose `5.5.1` on macOS/ARM64. Container runtime checks executed on Linux/ARM64.
The [SDK audit](../research/preparation-sdk-audit.md) separates adopted package contracts from newer
sibling source. This increment changes no shared SDK source.

## Preparation checks

| Check | Executed result |
|---|---|
| Locked NuGet restore and Release compilation | Passed; zero build warnings/errors |
| Backend unit suite | 14 passed; no failures or skips |
| Backend E2E suite | 28 passed against an isolated PostgreSQL container; no failures or skips |
| Frontend suite | 37 passed |
| Frontend static checks | ESLint, strict Vue/TypeScript, SFC compilation, and architecture checks passed |
| Fresh default API publish | Passed to `/private/tmp/brand-workspace-r02-final`; default `BuildSpa` ran `npm ci` and Vite |
| Frontend package audit during publish | Zero audited vulnerabilities; upstream deprecation notices remain |
| API and worker image builds | Passed with locked restore and the final frontend assets |
| Native media decoder in API image | Build-time `--verify-media-runtime` passed as the unprivileged runtime user |
| Migrations | Three application migrations applied; repeated packaged migration reported three applied, no new scripts |
| Packaged foundation smoke | Health, worker heartbeat, SPA root/deep link, and JSON404 across HTTP methods passed |
| Packaged preparation smoke | HTTPS sign-in, CSRF, organization isolation, image bytes, stale-save409, review, approval, ZIP idempotency, revocation, and logout passed |
| Container replacement | Same authenticated session and identical ZIP bytes survived API replacement with retained volumes; subsequent logout revoked the session |
| Final browser replay | Real sign-in → organization → brand → image upload → draft → review → approval → ZIP download → logout passed; dirty navigation retained edits |
| Responsive browser and screenshots | 1440px desktop and 360px mobile passed; no overflow, clipped header actions, console errors, or page errors |

The final backend reports are the `native-provider-fixed` TRX files under
`/private/tmp/brand-workspace-test-results/native-provider-fixed/`, ending in
`20260926033936.trx` (14 unit) and `20260926033947.trx` (28 E2E). These temporary artifacts are local
evidence; the checked-in suites and deployment scripts provide reproducible checks.

Backend integration coverage includes durable session expiry/revocation, account and security-stamp
changes, cross-account login, session-bound antiforgery, key-ring restart and discriminator isolation,
live organization membership, owner/editor/viewer authority, malformed JSON/multipart, foreign resources,
stale/concurrent writes, immutable revisions, exact approval, export conflicts, and revocation history.
The image tests validate real PNG/JPEG decoding, bounds, malformed payloads, and preserved source bytes.
The CLI media probe additionally proves verification can run without starting HTTP or connecting to
the deliberately unreachable database configured by its regression test.

Frontend coverage includes public SDK integration, multipart uploads, principal/organization lifecycle,
cross-tab and history restoration handling, draft conflicts, pending-write navigation, dirty edits,
SDK shell composition, and existing foundation diagnostics. Tests exercise the published package
APIs rather than substitute local sibling exports.

## Packaged observations

Only this task's `brand-workspace` Compose project was operated. The API uses a dedicated local PFX
through the optional HTTPS overlay at `https://localhost:8269`. The Python smoke client explicitly
trusted that test certificate while retaining chain and hostname checks. Host browser verification
uses the existing trusted .NET development certificate at `https://localhost:8264`; no global trust
settings were changed and no TLS-verification bypass was used.

The final packaged workflow created synthetic organizations `57b33fa9-8b7e-48dc-b997-358d7c494c3e`
and `99b8a4ec-1225-41b7-bc67-2f0599eb4055`, draft `9935a69b-8c5c-4840-88f6-dd34828a461e`, and
export `80bd3345-9929-428a-a720-ce2175dd070e`. The ZIP contained the exact caption and original PNG.
Revocation blocked a new export while the prior snapshot stayed byte-identical. After replacing the
API and worker containers, the original authenticated session and that export remained usable through
the retained database and Data Protection volumes. Logout then removed that session's authority.

SkiaSharp managed and normal Linux native packages both resolve to `4.152.1`; the final image includes
`libfontconfig1`. The build-time decoder returns `mediaRuntime:ready` and known PNG SHA-256
`5ce44d3259b5bf4b369481e556ceb140f338020b55a8a7ad70214b24da15bfe6`.
The test executes the production validator inside the final runtime image, so successful compilation
alone cannot hide a native library mismatch. Only Linux/ARM64 was executed locally; other runtime
identifiers were inspected for asset consistency but were not executed.

## Retained foundation evidence

The initial scaffold verified all 63 selected template files against their SHA-256 manifest before
sanitized staging. Its 30 tests comprised five backend unit, eight backend E2E, and 17 frontend cases.
That earlier snapshot is recorded in `d4b1add`; the current suites above supersede those counts.

The packaged queue durability check submitted job `1`, probe
`98e1fbde-4034-4f5a-bca3-2561f2262a54`, while the worker was stopped. It remained enqueued through a
PostgreSQL restart with the volume retained, then the replacement worker completed the same job at
`2026-09-25T21:09:45.448642+00:00`. The current backend suite retains its migration/worker durability
tests. This proves technical queue persistence, not exactly-once social-provider effects.

## Operational limits and continuation

- Local test accounts use `example.test`, synthetic copy, and a one-pixel image. No real Ocharo account,
  media, source record, or membership was imported. Secrets and certificates remain in ignored local storage.
- Operators can create organizations, but public registration, email delivery, password recovery,
  membership administration, and two-person approval policy are not implemented in this interface.
- PNG/JPEG originals are bounded and stored in PostgreSQL. Production object storage, retention,
  backups/restoration, key encryption, certificate rotation, and deployment acceptance remain separate work.
- Source media, drafts, review, approval, and immutable manual exports are implemented. Provider
  credentials, scheduling, automated delivery, and engagement measurements remain later work.
- A prepared ZIP records `publicationStatus:unverified`; its creation or download proves no publication.
- Local image builds do not establish remote GitHub Actions execution or production rollout.
- The developer's review is the final unchecked task in `r0.2`. Continued work follows the ordered
  [backlog](backlog.md), with explicit provider/account/content scope before any live integration.

The downloaded browser ZIP independently matched the original PNG byte-for-byte and the exact saved
caption; its manifest retained `publicationStatus:unverified`. The final local screenshots are
`/private/tmp/brand-workspace-r02-desktop.png` and `/private/tmp/brand-workspace-r02-mobile.png`.
Mobile geometry at scroll zero placed the header bottom and main top at the same 125px boundary.

The owned host API, packaged API/worker, and PostgreSQL sessions were stopped cleanly after verification.
The database and Data Protection volumes remain intact; no running preview is implied by this record.
Reproduction commands live in [deployment](../deployment/deployment.md) and
[operator access](../deployment/operator-access.md).

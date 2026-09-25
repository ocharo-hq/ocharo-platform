# Verification

*Last updated: 2026-09-26*

## Input state

The developer's baseline is `04a13f5952e15d3ee862647fe0f8757a42046416` (`Initial commit`).
The initial `.git` and `origin` were preserved. The implementation is the scaffold commit following
that baseline; no remote push, release, social account connection, or provider publication occurred.

All 63 selected template files matched the recorded SHA-256 manifest before sanitized staging.
The adapted source uses published backend SDK `10.0.58-beta` and Vue SDK `0.0.6`, eight NuGet
lockfiles, and `package-lock.json`. Verification used .NET SDK `10.0.300`, Node `24.11.0`, npm
`11.6.1`, Docker `29.8.0`, and Compose `5.5.1` on local macOS/ARM64.

## Executed checks

| Check | Result |
|---|---|
| Locked NuGet restore and backend/test compilation | Passed, zero build warnings/errors |
| Backend unit tests | 5 passed: missing/fresh/old/future heartbeat classification |
| Backend E2E tests | 8 passed: JSON API fallback, unavailable database, host filtering, migration/worker durability |
| Frontend install | `npm ci` passed; container install reported zero audited vulnerabilities |
| Frontend static checks | ESLint, strict Vue/TypeScript, runtime SFC compilation, architecture tests passed |
| Frontend tests | 17 passed: status decoding, HTTP failures/cancellation, loading/refresh/recovery, boundaries |
| Default clean API publish | Passed; absent generated `wwwroot` rebuilt through the default `BuildSpa` target |
| Published assets | API DLL, index, and every referenced JavaScript/CSS asset present in fresh publish output |
| Docker image builds | API and worker built successfully with locked NuGet restores; API uses frozen npm install |
| Compose initial migration | Exit 0; applied `001-foundation` and initialized Hangfire-owned schema |
| Packaged smoke check | Health, status/heartbeat, root/deep-link SPA, JSON404 across GET/POST/DELETE passed |
| Packaged restart recovery | Same queued probe survived PostgreSQL restart and completed on a replacement worker |
| Headless browser | Packaged SPA hydrated; refresh made another successful request; no JavaScript/console errors |
| Responsive browser check | 1440px desktop and 360px mobile readable without horizontal overflow |
| Registration and documents | Active IDE targets exist; Markdown links, workflow YAML, Compose configuration, whitespace checked |

The backend E2E suite owns an isolated PostgreSQL container. It proves ordinary API startup does not
create schema, repeated explicit migration is safe, API composition has no job client/server,
client-only composition has no hosted services, and worker/database replacement preserves queued work.
Probe receipts are idempotent. These tests do not establish exactly-once provider effects.

The clean publish ran with the API `wwwroot` moved aside and a new empty publish directory:

```bash
dotnet publish BrandWorkspace.Api/BrandWorkspace.Api.csproj -c Release --no-restore -m:1 -o <fresh-output>
```

`BuildSpa` performed `npm ci`, strict checks, and production Vite build. Generated assets were then
included in the final output. Native execution was used for the required local build, VSTest, Docker,
HTTP, and browser checks where sandbox process/socket restrictions prevented execution.

## Packaged durability observation

Only this task's `brand-workspace` Compose stack was operated. With its API running and worker stopped,
CLI submission returned job `1`, probe `98e1fbde-4034-4f5a-bca3-2561f2262a54`.
Status was `Enqueued` with a null receipt both before and after restarting PostgreSQL while retaining
its named volume. The replacement worker completed that same job with `Succeeded` and receipt
`2026-09-25T21:09:45.448642+00:00`. The full packaged smoke check passed again afterward.

The replacement worker image explicitly includes `libgssapi-krb5-2`; startup and CLI verification
completed without the earlier missing-native-library message. API health does not imply worker
readiness: the independent heartbeat becomes stale when the worker stops, and the UI displays that
observation honestly.

## Adoption limits and remaining product work

- The published Vue SDK lacks the local source's `AppQueryClient.dispose`; this consumer uses its
  published `clear()` contract. No mutable sibling source dependency was introduced.
- The backend SDK registration couples Hangfire client/server; normal worker composition uses it.
  Client-only diagnostics use upstream public composition at the executable boundary. Shared presets
  for client-only registration and schema control remain a library follow-up, not a product scheduler.
- Authentication, organization membership/isolation, brand data, drafts, immutable approvals, media,
  manual export, publication schedules, provider connections, and engagement observations are unimplemented.
- No production rollout, backup restoration, real provider access, image publication, or remote CI run
  was verified. Local workflow YAML validation and builds do not establish GitHub Actions execution.
- Developer acceptance remains the final unchecked item in `r0.1`; all technical scaffold tasks are verified.

The local verification stack is stopped at task completion with its database volume retained. Reproduce
it using [deployment commands](../deployment/deployment.md); no persistent runtime is implied by this record.

# Foundation HTTP and durable worker tests

*Last updated: 2026-11-09*

Run `dotnet test tests/BrandWorkspace.Tests.E2E` from the backend solution directory.
Docker must be available; the suite starts its own PostgreSQL 18 container through the SDK test fixture.
It checks JSON API fallbacks, DB readiness, absence of API job processing, migration repeatability,
queue persistence through a PostgreSQL container restart, worker host replacement, and idempotent receipts.
`DeploymentHostTests` start Desk as a Production target over an empty database (it migrates before it is
ready), refuse startup on drifted schema history, and apply forwarded headers only from the configured ingress.
`LocalRunTests` cover the SDK settings files and the local-run gating of the key ring.
Test hosts sign in through `DeskTestHost`, a stand-in for the Identity BFF cookie scheme driven by the `X-Test-Person` header;
no call reaches an Identity instance, and `AccessIdentityTests` cover the claims-based organization access.
`RuntimeConfigTests` serve the configured `OcharoApps:*` addresses anonymously and `no-store`, and drop malformed ones.
`FilesMediaTests` run Desk on WoW2 Files through `TestFiles`, an in-memory broker set: each media kind lands once, routes
redirect or answer bytes, rows stored before Files read their bytes until the boot backfill, and an unreachable Files
spares the boot.
No shared development database is changed. The suite does not contact social providers.

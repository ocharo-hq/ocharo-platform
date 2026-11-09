# Foundation HTTP and durable worker tests

*Last updated: 2026-11-09*

Run `dotnet test tests/BrandWorkspace.Tests.E2E` from the backend solution directory.
Docker must be available; the suite starts its own PostgreSQL 18 container through the SDK test fixture.
It checks JSON API fallbacks, `/health` readiness, the status version, cross-origin embedding of the public brand routes,
absence of API job processing, `migrate` verb repeatability, queue persistence through a PostgreSQL container restart,
worker host replacement, and idempotent receipts. `MediaRuntimeVerificationTests` run `media verify` through
`ServiceHost`, as the image build does.
`DeploymentHostTests` start Desk as a Production target over an empty database (it migrates before it is
ready), refuse startup on drifted schema history, and apply forwarded headers only from the configured ingress.
`LocalRunTests` cover the SDK settings files and the local-run gating of the key ring.
Test hosts sign in through `DeskTestHost`, a stand-in for the Identity BFF cookie scheme driven by the `X-Test-Person` header;
no call reaches an Identity instance, and `AccessIdentityTests` cover the claims-based organization access.
`IdentityCutoverTests` migrate a fresh database through `013-identity-cutover`, and one a deployed v0.2.3 left at
`012-files` with rows in every retired sign-in table, as a deployed run; the retired tables go, people and their
records stay, and the reserved `011` is refused there. `ImportVerbTests` run `import events` and `import sourcing`
through `ServiceHost`: `--actor` resolves by Identity user id or email, and an unknown person or organization refuses.
`RuntimeConfigTests` serve the configured `OcharoApps:*` addresses anonymously and `no-store`, and drop malformed ones.
`FilesMediaTests` run Desk on WoW2 Files through `TestFiles`, an in-memory broker set: each media kind lands once, routes
redirect or answer bytes, rows stored before Files read their bytes until the boot backfill, and an unreachable Files
spares the boot.
No shared development database is changed. The suite does not contact social providers.

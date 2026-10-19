# Foundation HTTP and durable worker tests

*Last updated: 2026-10-19*

Run `dotnet test tests/BrandWorkspace.Tests.E2E` from the backend solution directory.
Docker must be available; the suite starts its own PostgreSQL 17 container through the SDK test fixture.
It checks JSON API fallbacks, DB readiness, absence of API job processing, migration repeatability,
queue persistence through a PostgreSQL container restart, worker host replacement, and idempotent receipts.
`DeploymentHostTests` start Desk as a Production target over an empty database (it migrates before it is
ready), refuse startup on drifted schema history, and apply forwarded headers only from the configured ingress.
`LocalRunTests` cover the SDK settings files and the local-run gating of Telegram polling and the key ring.
No shared development database is changed. The suite does not contact social providers.

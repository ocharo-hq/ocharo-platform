# Foundation HTTP and durable worker tests

Run `dotnet test tests/BrandWorkspace.Tests.E2E` from the backend solution directory.
Docker must be available; the suite starts its own PostgreSQL 17 container through the SDK test fixture.
It checks JSON API fallbacks, DB readiness, absence of API job processing, migration repeatability,
queue persistence through a PostgreSQL container restart, worker host replacement, and idempotent receipts.
No shared development database is changed. The suite does not contact social providers.

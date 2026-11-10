# OcharoEvents.Tests.E2E

*Last updated: 2026-11-10*

Whole HTTP flows through the real host: `EventsServiceRouteTests` (Desk's Events routes moved here: list, summary,
tracking save, 410 on fact edits and deletes, proposals, another organization's path, the client wiring, 503 in a local
run without Events), `AccessTests` (401 without a session, 404 for another organization's path or a member without
`events:read`, 403 on writes without `events:track`, CSRF, uncached session routes), `RuntimeConfigTests`
(`OCHARO_EVENTS_URL`), `SystemStatusTests`, `SessionStoreTests` (sessions and key ring survive a restart) and
`StartupTests` (a deployed host refuses to boot without `Events:BaseUrl` or its database).

Prerequisites: the .NET 10 SDK and Docker. Each host gets a fresh PostgreSQL product database from `Harness/EventsTestDatabase`
(one `postgres:18-alpine` container per run, Testcontainers), WoW2 Events is `Harness/FakeEventsService` in memory, and
sign-in is `Harness/EventsTestIdentity`, a stand-in for the Identity BFF cookie scheme driven by the `X-Test-Person`
header, so no call reaches Identity or Events. Run `dotnet test tests/OcharoEvents.Tests.E2E` from the backend solution
directory.

# OcharoEvents.Tests.E2E

*Last updated: 2026-11-20*

Whole HTTP flows through the real host: `EventsApiRouteTests` (the Events page's mirror of WoW2 Events, OE07: one
Events call per list with the kit's filters and cursor, summary kinds, tracking writes and history, proposals, the
workflow and the calendar file passed through, the feed's `cities`, `kinds`, `sources`, `status`, `publishedSince` and
`sort` passed through, the tracking facets read (UI06: one Events call with the feed's query, each facet counted without its own
selection by the harness's fake, 404, 401 and 400 refusals, an Events 400 for an old cursor reaching the page as 400), and a member's
freshness read of the catalog's intake (E24: one Events call, Events' 404 reaching the page); 403 for a viewer's write and an editor's workflow write, 404 for another
catalog or organization, 400 for a malformed filter, all before Events; Events' refusal reaching the page; the client
wiring; 503 in a local run without Events), `AccessTests` (401 without a session, 404 for another organization's path or
a member without `events:read` (the freshness and facets reads included), 403 on writes without `events:track`, CSRF, uncached session routes),
`EventImageRouteTests` (the event image route: 302 to the signed Files URL of the copy the event named, with `private, max-age=60`; the file id from the event, never the browser; 404 for an index out of range and an image with no `fileRef`; 302 to the image's https source link when Files is unconfigured, refuses, is unreachable or times out, and 404 when that link is not https; 401 without a session; 404 for another organization, a member without `events:read` and another catalog; Events' refusal passed on),
`TelegramDeliveryRouteTests`, `RuntimeConfigTests` (`OCHARO_EVENTS_URL`), `SystemStatusTests`, `SessionStoreTests`
(sessions and key ring survive a restart) and `StartupTests` (a deployed host refuses to boot without `Events:BaseUrl` or
its database).

Prerequisites: the .NET 10 SDK and Docker. Each host gets a fresh PostgreSQL product database from `Harness/EventsTestDatabase`
(one `postgres:18-alpine` container per run, Testcontainers), WoW2 Events is `Harness/RecordingEventsBrokers` (each call recorded, a canned answer; the facets read counts its fake `Tracked` events per stage, kind, city and country) `Harness/FakeDigestsBroker` and `Harness/FakeFileUrlsBroker` (WoW2 Files' signed URLs, each request recorded), and
sign-in is `Harness/EventsTestIdentity`, a stand-in for the Identity BFF cookie scheme driven by the `X-Test-Person`
header, so no call reaches Identity or Events. Run `dotnet test tests/OcharoEvents.Tests.E2E` from the backend solution
directory.

# OcharoEvents.Tests.Unit

*Last updated: 2026-11-10*

Pure logic with no I/O, moved from Desk with the Events slice: `EventsServiceTests` (WoW2 Events' catalog and tracking
mapped onto the page's record through `FakeEventsService`, tracking writes, Gone and Conflict, cursor, filters and
summary), `EventValidationTests` (field normalization and date rules) and `OcharoAppsSettingsExtensionsTests` (which
`OcharoApps:*` addresses the runtime config may publish).

Prerequisites: none. Run `dotnet test tests/OcharoEvents.Tests.Unit` from the backend solution directory.

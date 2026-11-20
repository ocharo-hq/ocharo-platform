# OcharoEvents.Tests.Unit

*Last updated: 2026-11-20*

Pure logic with no I/O: `EventsQueryParametersTests` (the mirror reads the kit's query into WoW2 Events' own, every
filter and default as Events' routes read them, the feed's `kinds`, `cities`, `sources`, `status`, `publishedSince` and
`sort` included, a malformed day, instant or wire name refused; the catalog guard) and
`OcharoAppsSettingsExtensionsTests` (which `OcharoApps:*` addresses the runtime config may publish) and
`EventImagesServiceTests` (the event image route's lookup: the Files request built from the event's own
`facts.images[index].fileRef` for five minutes inline; an index out of range, an image with no `fileRef` and another catalog answer not found
without asking Files; Files unconfigured, refusing, throwing or too slow, or a `fileRef` that is no file id, answers the
image's own https source link, and not found when that link is not absolute https or carries credentials; a caller's
cancellation still cancels).

Prerequisites: none. Run `dotnet test tests/OcharoEvents.Tests.Unit` from the backend solution directory.

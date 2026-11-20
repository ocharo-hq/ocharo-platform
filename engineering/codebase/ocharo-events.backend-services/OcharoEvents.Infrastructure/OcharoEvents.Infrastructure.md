# OcharoEvents.Infrastructure

*Last updated: 2026-11-20*

The pass-through to WoW2 Events' brokers. `Events/Services/EventsApiService` serves the Events page's mirror (tracking and its
facet counts, one event, proposals, the workflow and the calendar file), `Delivery/Services/EventDigestsService` the organization's
Telegram delivery from Events' digests; both refuse a catalog other than `Events:Catalog` (404) and an unconfigured
Events (503) through `EventsCatalogGuard` before any call, and pass everything else as the page sent it (OE07).
`Events/Services/EventImagesService` finds the link to SCFD's stored copy of an event's image: it reads the event through
`EventsApiService`, takes `facts.images[index].fileRef` and asks WoW2 Files' `IFileUrlsBroker` (read-only tenant key,
`FilesServiceOptions`: `Files:BaseUrl`, `Files:ApiKey`) for a 5-minute signed URL; an index out of range or an image with no `fileRef` is 404, and Files unconfigured, failing,
throwing or slower than `FilesServiceOptions.LinkTimeout` (5 s) answers the image's own absolute https `url` instead (404 for
any other source link).

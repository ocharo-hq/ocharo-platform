# OcharoEvents.Infrastructure

*Last updated: 2026-11-20*

The pass-through to WoW2 Events' brokers. `Events/Services/EventsApiService` serves the Events page's mirror (tracking and its
facet counts, one event, proposals, the workflow and the calendar file), `Delivery/Services/EventDigestsService` the organization's
Telegram delivery from Events' digests; both refuse a catalog other than `Events:Catalog` (404) and an unconfigured
Events (503) through `EventsCatalogGuard` before any call, and pass everything else as the page sent it (OE07).

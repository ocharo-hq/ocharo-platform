# Events

*Last updated: 2026-11-19*

The Events page's mirror of WoW2 Events (OE07): `EventsApiController` serves the `fe-events` kit's routes under
`api/organizations/{organizationId}/events-api/api/events/v1/…`, so the page's client reaches them with that prefix,
as the Telegram delivery page reaches the digest routes under `…/delivery`. Each route binds Events' own query
(`Models/TrackingQueryParameters`, `Models/EventQueryParameters`: lists as repeated keys, days `yyyy-MM-dd`, enums by
wire name) or body (`TrackingSpec`, `EventSpec`, `WorkflowSpec`), calls one broker through `EventsApiService` and
answers Events' model in the SDK envelope; no Ocharo wire, mapper or in-memory filter sits between the page and Events.

| Method | Route (after `…/events-api/api/events/v1`) | Needs |
|---|---|---|
| GET | `catalogs/{catalog}/tracking` (feed: stages, countries, kind, text, from/to, cursor, limit) | `events:read` |
| GET | `catalogs/{catalog}/tracking/summary` (`today`, repeated `deadlineKinds`) | `events:read` |
| GET, PUT, DELETE | `catalogs/{catalog}/tracking/{eventId}` (`expectedRevision` on delete) | read; writes `events:track` |
| GET | `catalogs/{catalog}/tracking/{eventId}/history` | `events:read` |
| GET | `catalogs/{catalog}/events/{id}` | `events:read` |
| POST | `catalogs/{catalog}/candidates` (published at once in Ocharo's private catalog, OE03) | `events:track` |
| GET, PUT | `workflow` (the page aligns Ocharo's labels, classes and fields for an admin) | read; write `events:admin` |
| GET | `catalogs/{catalog}/events.ics` (calendar file, no envelope) | `events:read` |

A catalog other than `Events:Catalog` answers 404 and an unconfigured Events 503 before any call; Events checks the
exchanged token's scopes and permissions again.

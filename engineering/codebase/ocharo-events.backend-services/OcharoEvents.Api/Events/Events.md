# Events

*Last updated: 2026-11-20*

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
| GET, PUT | `workflow` (the page reads it; the deploy writes Ocharo's with Events' `workflow put --spec`, so no app caller of `PUT` remains and it stays for an admin's own edits) | read; write `events:admin` |
| GET | `catalogs/{catalog}/events.ics` (calendar file, no envelope) | `events:read` |
| GET | `catalogs/{catalog}/freshness` (`lastReceivedAtUtc`, `newestObservedAtUtc`, `failingSources` of the catalog's intake, E24) | `events:read` |

A catalog other than `Events:Catalog` answers 404 and an unconfigured Events 503 before any call; Events checks the
exchanged token's scopes and permissions again.

The freshness read (E24) serves the page's own "Sources updated {time}" line, not a kit screen: the page calls the kit's
`getIntakeFreshness`, and the route makes one `IEventIntakeBroker.GetFreshnessAsync` call for the signed-in member, so
Events' member check (`events.read` and `events:read` in the catalog's owner organization) holds on the exchanged token;
another organization, or a person with no organization, reads 404 from Events as from this service, and the page shows
nothing then.

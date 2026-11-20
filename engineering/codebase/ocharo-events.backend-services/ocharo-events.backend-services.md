# ocharo-events.backend-services

*Last updated: 2026-11-20*

Ocharo Events' backend (`research/events-app.md` #1–#9): one host serving the Events page's mirror of WoW2 Events
(OE07), the Identity BFF and the SPA built from `../ocharo-events.frontend-services/`. It keeps no event tables: WoW2 Events holds the catalog and each
organization's tracking, reached by the signed-in person's exchanged token. PostgreSQL holds only the Data Protection key
ring (schema `ocharo_events`) and the BFF sessions (schema `ocharo_events_cache`).

It also serves SCFD's stored copy of an event's image: a route beside the mirror reads the event, takes the Files id from
`facts.images[index].fileRef` and redirects (302) to a short-lived signed WoW2 Files URL, by a read-only tenant key
(`Files:BaseUrl`, `Files:ApiKey`, both optional: with either missing, or Files failing, it redirects to the image's own https
source `url` instead).

Projects: `OcharoEvents.{Api,Application,Domain,Infrastructure,Persistence}`, tests in `tests/OcharoEvents.Tests.{Unit,E2E}`.
Run the API with the `https` launch profile on `https://localhost:8290`; the frontend dev server on `8292` proxies to it.
Prerequisites for the E2E tier: Docker (Testcontainers PostgreSQL).

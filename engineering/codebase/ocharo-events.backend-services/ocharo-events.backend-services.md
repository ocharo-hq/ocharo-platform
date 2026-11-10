# ocharo-events.backend-services

*Last updated: 2026-11-10*

Ocharo Events' backend (`research/events-app.md` #1–#9): one host serving the Events API, the Identity BFF and the SPA
built from `../ocharo-events.frontend-services/`. It keeps no event tables: WoW2 Events holds the catalog and each
organization's tracking, reached by the signed-in person's exchanged token. PostgreSQL holds only the Data Protection key
ring (schema `ocharo_events`) and the BFF sessions (schema `ocharo_events_cache`).

Projects: `OcharoEvents.{Api,Application,Domain,Infrastructure,Persistence}`, tests in `tests/OcharoEvents.Tests.{Unit,E2E}`.
Run the API with the `https` launch profile on `https://localhost:8290`; the frontend dev server on `8292` proxies to it.
Prerequisites for the E2E tier: Docker (Testcontainers PostgreSQL).

# Delivery

*Last updated: 2026-11-17*

The organization's Telegram delivery (tasks-platform.md O17): WoW2 Events' scheduled digests of the Events page's catalog,
mirrored under `api/organizations/{organizationId}/delivery/api/events/v1/catalogs/{catalog}/digests`, so the page's
`fe-events` editor (`DigestsPanel`) calls them with the prefix `/api/organizations/{organizationId}/delivery`.

- Routes: list, read, create (201), replace at `expectedRevision`, delete (`?expectedRevision=`), `{id}/preview` (renders
  without sending), `{id}/send` (send now) and `{id}/runs` (history, `cursor`, `limit`); bodies and answers are Events'
  digest models in the SDK envelope.
- Access: the organization filter as for events (404 outside the session's organization or without `events:read`), then
  403 without `events:admin`; another catalog than `Events:Catalog` answers 404 and an unconfigured Events 503, both
  before Events.
- Each call exchanges the person's session token for Events' audience, as the events routes do; Events checks the
  `events.admin` scope (client 11) and `events:admin` again, keeps each digest to the token's organization, and sends
  through Ocharo's Identity bot as client `events` (`identity.notices.send`).

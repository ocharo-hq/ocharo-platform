# Presentation

_Last updated: 2026-11-19_

Pages and their parts in SDK components. `events/pages/EventsPage` renders WoW2 Events' own screens from `fe-events`'
kit with Ocharo's configuration (OE07): the nearest application deadlines (`EventsDeadlines`), the organization's
tracking feed (`EventsFeed`: skipped and rejected hidden until asked, scope chips, quick Shortlist and Skip, Ocharo's
cost, "Add event" publishing at once), "Past, not closed" (`events/PastEventsStrip`: ended events Ocharo was pursuing, each
closed as Attended or Skipped in one tracking write, OE12) and one event's detail with its tracking form (`EventDetail`); event facts come
from their source and are never edited here. The session banner and the app menu icons; `delivery/` is the admins'
Telegram delivery page, embedding `fe-events`' digest editor (`DigestsPanel`) with Ocharo's stages, and offering the
"Deadline reminders" digest, added switched off.

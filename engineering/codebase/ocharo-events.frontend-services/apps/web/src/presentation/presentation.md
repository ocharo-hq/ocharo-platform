# Presentation

_Last updated: 2026-11-20_

Pages and their parts in SDK components. `frame/` is the sidebar (`EventsNav`: the feed and the calendar, then Settings with
the reminders and, for an admin, the Telegram delivery; an icon rail when collapsed) and the phone's bottom bar
(`PhoneNav`: the same places, none behind a menu). `events/pages/EventsPage` is the feed, rendering WoW2 Events' own
screens from `fe-events`' kit with Ocharo's configuration (OE07): the organization's tracking feed (`EventsFeed`: skipped
and rejected hidden until asked, scope chips, quick Shortlist and Skip, Ocharo's cost, "Add event" publishing at once) and
one event's detail with its tracking form (`EventDetail`); event facts come from their source and are never edited here.
Under the feed's title, `events/AttentionPills` folds the nearest application deadlines (`EventsDeadlines`) and "Past, not
closed" (`events/PastEventsStrip`: ended events Ocharo was pursuing, each closed as Attended or Skipped in one tracking
write, OE12) behind two pills with their counts, gone at zero; they are interim, until the feed gives them slots.
`events/SourcesFreshness` ("Sources updated {time}", a warning when the last record is over 24 hours old or a source
fails, nothing when the read fails, E24) stands at the sidebar's foot, as its icon alone on the rail, and as that icon beside the
feed's title below `md`, where there is no sidebar, from the one read the frame makes. `events/pages/CalendarPage`
puts the organization's events on the SDK calendar (`EventCalendarViewer`), coloured by Ocharo's stage, a block opening the
event in the feed; `reminders/pages/RemindersPage` says the reminder settings are on their way. Each place has a route
wrapper (`*Route`) that serves only the session's own organization, to a person who may read it, and answers anything else as
not found. The session banner and the app menu icons; `delivery/` is the admins' Telegram delivery page, embedding
`fe-events`' digest editor (`DigestsPanel`) with Ocharo's stages.

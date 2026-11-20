# Presentation

_Last updated: 2026-11-20_

Pages and their parts in SDK components. `frame/` is the sidebar (`EventsNav`: the feed and the calendar, then Settings with
the reminders and, for an admin, the Telegram delivery; an icon rail when collapsed) and the phone's bottom bar
(`PhoneNav`: the same places, none behind a menu). `events/pages/EventsPage` is the feed, rendering WoW2 Events' own
screens from `fe-events`' kit (0.0.14) with Ocharo's configuration (OE07): the organization's tracking feed (`EventsFeed` v2: a
facet bar with the match pill "n of N", search, scope and kind chips and a Filters popover, all with server counts, a sort, a
grid of the kit's `EventMediaCard`s that loads more on scroll with month dividers under the start-date sort; skipped and
rejected hidden until asked, quick Shortlist and Skip in each card's menu, "Add event" publishing at once, Ocharo's cost
left to the detail) and one event's detail (`EventDetail`: the facts and sources, with no tracking card until it is redesigned,
the page leaving the kit's `trackable` off); event facts come from their source and are never edited here. An image SCFD stored a copy of shows from the backend's image route on the card and in the detail (the page's client gives the kit the route as the image's address); an image with no copy keeps the kit's own rendering, the source image and then the date tile. Until the backend serves the copy (no Files key yet, or Files failing), the route redirects to the image's own source link, so the card shows the source photo as before. The kit's own heading is for screen readers only (`sr-only`), so the page's "Feed" is the one title.
Between the bar and the grid sit the feed's attention pills, hidden at zero: the kit's own application deadlines in the next
14 days, and Ocharo's "past to close", passed in through the `attention` prop whose `#attention` slot is
`events/PastEventsStrip`: the ended events Ocharo was pursuing, each closed as Attended or Skipped in one tracking write
(OE12). The strip only shows what the page's `usePastToClose` holds, since the pill needs the count before it unfolds.
`events/SourcesFreshness` ("Sources updated {time}", a warning when the last record is over 24 hours old or a source
fails, nothing when the read fails, E24) stands at the sidebar's foot, as its icon alone on the rail, and as that icon beside the
feed's title below `md`, where there is no sidebar, from the one read the frame makes. `events/pages/CalendarPage`
puts the organization's events on the SDK calendar (`EventCalendarViewer`), coloured by Ocharo's stage (an event not yet
tracked a quiet tint of the info colour, a pursued one a stronger mix of the primary), a block opening the event in the feed; three scope pills above it (the SDK's `ToggleGroup`, several at once: National, Central Asia, International, each with the number of events the loaded window draws in it, none chosen showing every event) narrow the events to those whose country is in a chosen scope;
`reminders/pages/RemindersPage` says the reminder settings are on their way. Each place has a route
wrapper (`*Route`) that serves only the session's own organization, to a person who may read it, and answers anything else as
not found. The session banner and the app menu icons; `delivery/` is the admins' Telegram delivery page, embedding
`fe-events`' digest editor (no back link: the sidebar leads to the feed) (`DigestsPanel`) with Ocharo's stages.

# Presentation

_Last updated: 2026-11-22_

Pages and their parts in SDK components. `frame/` is the sidebar (`EventsNav`: the feed and the calendar, then Settings with
the reminders and, for an admin, the Telegram delivery; an icon rail when collapsed) and the phone's bottom bar
(`PhoneNav`: the same places, none behind a menu). `events/pages/EventsPage` is the feed, rendering WoW2 Events' own
screens from `fe-events`' kit (0.0.22) with Ocharo's configuration (OE07): the organization's tracking feed (`EventsFeed` v2: a
grid of the kit's `EventMediaCard`s that loads more on scroll with month dividers under the start-date sort; skipped and
rejected hidden until asked, quick Shortlist and Skip in each card's menu, "Add event" publishing at once, Ocharo's cost
left to the detail) under the filter card it shares with the calendar (`events/EventFilterCard`; developer, 2026-11-22) and one event's detail (`EventDetail`: the facts and sources, with no tracking card until it is redesigned,
the page leaving the kit's `trackable` off); event facts come from their source and are never edited here. An image SCFD stored a copy of shows from the backend's image route on the card and in the detail (the page passes the kit's feed and detail an `imageSrc` resolver that returns the route); when the route fails to load the kit falls back to the image's source link and then to the date tile (card) or no image (detail), and an image with no copy keeps the kit's own rendering, the source image. Until the backend serves the copy (no Files key yet, or Files failing), the route redirects to the image's own source link, so the card shows the source photo as before. The kit's own heading is for screen readers only (`sr-only`), so the page's "Feed" is the one title.
The page holds the kit's filter state (`useEventsFilters`: the search, the choices and the sort, the facets with the server's counts,
the "n of N" pill), keeps it in the address (`filters.toRouteQuery()` written in place and `fromRouteQuery` read on load and on back
and forward, through `bindAddressFilters`; opening an event keeps the address's query), and hands it to the feed as `filters`: the feed
then draws no toolbar of its own and lists from that state, and its sort, "Propose an event" and overflow menu (the `events-actions`
row) move into the card's controls row through `actions-to`, naming the element the card makes for them (`actionsId`). Between the card and the grid sit the feed's attention pills, hidden at zero: the kit's own application deadlines in the next
14 days, and Ocharo's "past to close", passed in through the `attention` prop whose `#attention` slot is
`events/PastEventsStrip`: the ended events Ocharo was pursuing, each closed as Attended or Skipped in one tracking write
(OE12). The strip only shows what the page's `usePastToClose` holds, since the pill needs the count before it unfolds.
`events/pages/CalendarPage`
puts the organization's events on the SDK calendar (`EventCalendarViewer`), coloured by Ocharo's stage (an event not yet
tracked a quiet tint of the info colour, a pursued one a stronger mix of the primary), a block opening the event in the feed; the page itself carries no visible heading, only a screen-reader-only one, and under the top bar sits the same filter card (`events/EventFilterCard`) above the viewer, which keeps its own toolbar (Today, previous, next, the title and the view switcher). The calendar fills it with Region (All, National, Central Asia, International: one choice, as on the feed) and Kind as pills, Stage, City and Source behind "Filters" as removable chips, a search of titles, cities and organizers, "Clear all" and the match pill "n of m", each choice counted over the window the calendar has read, with the other facets applied (counts and the pill wait for the first read); the feed's counts come from the server for its upcoming range, and the two are never mixed. The calendar ignores the feed's date preset and sort.
`events/EventFilterCard` is the card both pages share: a `Card` of radius `2xl` whose padding shrinks on a phone, holding the SDK's `FacetBar`. A page hands it the facets with their counts (`facetDefinitions`), the kit's label props (`eventsFacetBarLabels`, so both pages read alike), the selection and the search (`v-model`) and the match and total counts, and takes the choices back; the card filters nothing and knows no page. It lays the bar out on a phone with the kit's own `feedFacetBarClass`. Inside it every region and kind pill opens a mini-card hint, one line from `domain/events/EventPillHints` (`withPillHints`), after 5 seconds on hover or focus with a 200 ms fade (named constants beside the table: the W2 tooltip convention is still to be found, these are the developer's starting values); a pill with a count of 0 that is not chosen greys itself (the bar's own rule) and stays pressable. `actionsId` makes an element at the end of the bar's controls for the feed's own controls; the calendar names none. Pill icons are not built yet.
`reminders/pages/RemindersPage` says the reminder settings are on their way. Each place has a route
wrapper (`*Route`) that serves only the session's own organization, to a person who may read it, and answers anything else as
not found. The session banner and the app menu icons; `delivery/` is the admins' Telegram delivery page, embedding
`fe-events`' digest editor (no back link: the sidebar leads to the feed) (`DigestsPanel`) with Ocharo's stages.

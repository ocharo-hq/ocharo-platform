# Tests

_Last updated: 2026-11-22_

Vitest unit and component tests over happy-dom; run `pnpm test` from the workspace root after `pnpm install`. No API, browser or
network is needed: each test stubs `fetch` (the session, status, runtime-settings and mirrored Events routes).

- `EventsPage.test.ts` — the feed on `fe-events`' kit (0.0.22, v2) through this app's mirror (OE07): the header as the title alone and the
  kit's own heading only for screen readers, one tracking request with skipped and rejected left out, the facet bar in the shared filter card (the "n of N" pill, Ocharo's
  scope chips (National, Central Asia, International) and kind chips and stages with the server's counts, one facets read per query change, no v1 `variant` or `shownFields`, the kit's own toolbar left out), the sort and the
  month dividers, scope chips and the hidden-stages choice sent server-side, titles in English, a quick Shortlist from the card's overflow menu,
  "Add event" (the country through the kit's picker), one event's detail with its facts and no tracking section (no stage, priority, notes, fields
  or history, and no tracking read), no workflow write on a visit (any role, any stored workflow), Ocharo's stage labels in the feed; and the attention pills between the bar and the cards: the kit's deadlines pill and its
  list with links, the past pill with its count, a past event closed from the slot's strip, no pill at zero, both read again
  after a write in the feed. Scroll loading stays the kit's own test.
  The filter card above the feed: one card holding the bar, with the kit's phone class, and the feed's sort, Propose and overflow controls in its one actions
  row inside the bar; the region and kind pills' hints, the 5 second open delay and 200 ms fade, a pill at count 0 greyed and still pressable; a chosen kind
  written to the address as `kinds` and the feed listed with it; the region, search, sort and the picks behind Filters under the kit's keys with the other query
  kept, and the defaults left out; the page opened on an address's filters and following it on back and forward; a search with a trailing space kept as typed; and
  the filters kept in the address while an event is open and when it is closed.
  Event images: a card whose image has a `fileRef` shows the backend route (not the source link, not the file id), lazy, with no referrer
  and the title as its alt text; a card with no image keeps the date tile; an image with no copy keeps the kit's own source image; a failed
  stored copy falls back to the source link and then the date tile, and a failed source image to the date tile; the detail shows each stored copy through the route of its own place.
- `EventImages.test.ts` — the image route's path under the organization's prefix, and the `imageSrc` resolver: an image with a `fileRef` resolves to
  the route, absolute against the origin, numbered by its index; one with no copy (none, null or blank) resolves to `null`.
- `CalendarPage.test.ts` — the calendar place: the visible month read through the feed's calls and paged by cursor (capped at 1000),
  all-day and month-only events, stage colours, a new read for the next month and none inside the days read, loading, failure with retry,
  a block opening its event in the feed; and the shared filter card: one card under the heading and above the viewer (a `Card` of radius `2xl`, padding
  shrinking on a phone), never in the viewer's toolbar, which keeps Today, previous, next, the title and the view switcher; Region (All, then the
  three regions, one choice at a time) and Kind as pills with their counts counted client-side over the window, the rest behind Filters, the match pill "n of m" and the
  kit's search placeholder; a chosen kind narrowing the drawn events and writing `kinds` to the address with nothing read again; a chosen region narrowing them
  while the region counts stay and the kind counts follow; the choices under the kit's keys (`regions`, `kinds`, `stages`, `cities`, `sources`, `q`) kept in the
  address with the other query, the feed's `when` and `sort` included, opened from the address the feed writes and followed when it changes, and a region that is none ignored;
  the search over titles, cities and organizers; Stage, City and Source as removable chips, a chip removed; "Clear all"; the hints, their timing and the greyed
  empty pills; the card shown with no counts and no pill during the bare loading skeleton and counted once the calendar mounts.
- `EventsCalendar.test.ts` — the calendar's model: the window each view reads, all-day blocks with an inclusive end, timed blocks in the
  event's zone shown in the browser's, events with no day and closed stages left out, titles in English, stage colours (theme variables only,
  never a hex; an untracked event never the muted surface), and an event's scope from its country: UZ National, KZ KG TJ TM Central Asia, RU AZ AF and the rest International, no country in none.
- `CalendarFilters.test.ts` — the calendar's filters over six drawn events and two left out: each facet's filter (region with its semantics, kind,
  stage with an untracked event as Candidate, city, source in any spelling and curated), several values at once, facets combined, the search (title in
  any language, city, organizer, case, blank); the counts (each facet counted with the others' choices and the search applied but not its own, the
  catalog's kinds and open stages with their labels, cities and sources most first, a kind or a chosen value the window lacks kept, an empty window,
  the match and drawn totals); the city and source values as Events stores them (trimmed, lowercase); and the address's reading and writing under the kit's keys
  (the region as the kit reads it: the first value, only when it is a region; the old keys and the feed's `when` and `sort` unread) and the facet bar's
  selection (the region as one value, the "All" pill choosing none).
- `EventFilterQuery.test.ts` — the page-neutral address keys and values: a repeated key read once each and trimmed with no split at a comma, nothing for an
  absent or blank key, the list written back as a list, the search text under `q`; the six keys both pages share and the feed's two more; and the shared keys of
  an address written after a path for a link to the other page, every other key left behind.
- `EventPillHints.test.ts` — the pill hints: the 11 one-liners (3 regions, 8 kinds) as the pills doc has them and covering every region and kind Ocharo
  offers, the 5 second open delay and 200 ms fade, scope and kind options decorated with `hint: { description }` while other facets, toggles and
  options with no line stay the same objects, and no hint for a value that is a member of every object.
- `EventsNavFilters.test.ts` — the Feed and Calendar links in the sidebar and on the phone bar carrying the open address's shared filter keys (`q`,
  `stages`, `kinds`, `cities`, `sources`, `regions`) and no other (`when`, `sort`, a stray key), clean when it holds none and for Reminders and Telegram
  delivery, and a filter set on the Calendar there on the Feed and back.
- `EventsSections.test.ts` — the sections, their groups and addresses, and Telegram delivery for an admin only.
- `RemindersPage.test.ts` — the reminder settings' placeholder.
- `UsePastToClose.test.ts` — "Past, not closed" (OE12): the query by stage and last start day, an ended confirmed event listed and a
  running or untracked one not, Attended or Skipped as one tracking write that keeps the rest, a conflict reloading the list, another failure
  kept with the list, and the count of events waiting.
- `PastEventsStrip.test.ts` — the strip (the attention pill's body) over what the hook holds: the ended events with their dates and stage, Attended
  and Skipped asking to close, a viewer offered no action, the changed notice, a failure with its reload, and an event opened in the page.
- `EventsModel.test.ts` — Ocharo's kit configuration (workflow, kinds, scopes with their keys, labels and countries, proposal) and the page's addresses; the
  workflow spec equals `deployment/events/ocharo-workflow.json`, and what the page shows of a stored workflow.
- `EventsFrame.test.ts` — the Ocharo frame: bar, version badge, account menu with versions and no language list, English kept when another is asked, apps menu; the sidebar's
  places for an admin and a member, the open one following the route, the icon rail's stored choice, the phone's bottom bar with every
  place and no More.
- `EventsRouter.test.ts` — `/` to the session organization's feed, the feed, calendar and reminders places, other organizations and missing
  permission as not found, and no Deadlines place.
- `OcharoAppsConfig.test.ts` — the Events entry of the apps menu and its runtime and build-time addresses.
- `TelegramDelivery.test.ts` — the digest editor's defaults (the digest named "Ocharo events", its content in Uzbek) and the mirrored digest routes; no "Deadline reminders" offer.
- `UiMessages.test.ts` — a phrase with no entry answers its English source (new strings need English only), existing entries keep a well-formed
  uz and ru text and their `{params}`, the kit's labels carry English, and no "Showcase events" string is left in the source.
- `UseUiLocale.test.ts` — the interface is English only (developer, 2026-11-20): a stored or browser language is ignored and left as it is, and a
  change to another language is a no-op that stores nothing.
- `ReplayQuery.test.ts` — replay mode's query engine (`dev/ReplayQuery.ts`, over a small inline fixture): a kind and a country filter (unknown countries only when
  let in), an untracked event read as the default stage, a date window (overlap, month precision, undated only when asked), sort by start, publication and deadline,
  facet counts without the facet's own selection, the summary by stage with its nearest deadlines, paging by cursor, and the query strings.
- `helpers/` — the app mount and the router.

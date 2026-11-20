# Tests

_Last updated: 2026-11-20_

Vitest unit and component tests over happy-dom; run `pnpm test` from the workspace root after `pnpm install`. No API, browser or
network is needed: each test stubs `fetch` (the session, status, runtime-settings and mirrored Events routes).

- `EventsPage.test.ts` — the feed on `fe-events`' kit (0.0.14, v2) through this app's mirror (OE07): the header as the title alone and the
  kit's own heading only for screen readers, one tracking request with skipped and rejected left out, the facet bar (the "n of N" pill, Ocharo's
  scope chips (National, Central Asia, International) and kind chips and stages with the server's counts, one facets read per query change, no v1 `variant` or `shownFields`), the sort and the
  month dividers, scope chips and the hidden-stages choice sent server-side, titles in English, a quick Shortlist from the card's overflow menu,
  "Add event" (the country through the kit's picker), one event's detail with its facts and no tracking section (no stage, priority, notes, fields
  or history, and no tracking read), no workflow write on a visit (any role, any stored workflow), Ocharo's stage labels in the feed; and the attention pills between the bar and the cards: the kit's deadlines pill and its
  list with links, the past pill with its count, a past event closed from the slot's strip, no pill at zero, both read again
  after a write in the feed. Scroll loading stays the kit's own test.
- `CalendarPage.test.ts` — the calendar place: the visible month read through the feed's calls and paged by cursor (capped at 1000),
  all-day and month-only events, stage colours, a new read for the next month and none inside the days read, loading, failure with retry,
  a block opening its event in the feed; and the scope pills: National, Central Asia and International above the calendar with the loaded window's counts
  (none before the first read), each narrowing the viewer's events to its country group, none chosen showing every event, those with no country included,
  the choice kept in `?scope=` (the regions' order, other query kept) and followed when the address changes, and an address naming no region ignored.
- `EventsCalendar.test.ts` — the calendar's model: the window each view reads, all-day blocks with an inclusive end, timed blocks in the
  event's zone shown in the browser's, events with no day and closed stages left out, titles in English, stage colours (theme variables only,
  never a hex; an untracked event never the muted surface), and the scopes: UZ National, KZ KG TJ TM Central Asia, RU AZ AF and the rest International, no country in none,
  the events kept by chosen scopes, the per-scope counts (closed stages, events with no day and no-country events counted in none), and the `?scope=` query's reading and writing.
- `EventsSections.test.ts` — the sections, their groups and addresses, and Telegram delivery for an admin only.
- `RemindersPage.test.ts` — the reminder settings' placeholder.
- `SourcesFreshness.test.ts` — "Sources updated {time}" (E24) from the state the frame offers: fresh, stale past 24 hours (none at exactly 24),
  failing sources, none before the first record, nothing before a read or outside the frame, and compact as an icon with
  its tooltip and name.
- `IntakeFreshnessRead.test.ts` — the one freshness read: once on mount, again every five minutes and on return to the tab, nothing while there
  is no client, null when it fails, and stopped with its component.
- `UsePastToClose.test.ts` — "Past, not closed" (OE12): the query by stage and last start day, an ended confirmed event listed and a
  running or untracked one not, Attended or Skipped as one tracking write that keeps the rest, a conflict reloading the list, another failure
  kept with the list, and the count of events waiting.
- `PastEventsStrip.test.ts` — the strip (the attention pill's body) over what the hook holds: the ended events with their dates and stage, Attended
  and Skipped asking to close, a viewer offered no action, the changed notice, a failure with its reload, and an event opened in the page.
- `EventsModel.test.ts` — Ocharo's kit configuration (workflow, kinds, scopes with their keys, labels and countries, proposal) and the page's addresses; the
  workflow spec equals `deployment/events/ocharo-workflow.json`, and what the page shows of a stored workflow; the 24-hour
  boundary of Events' intake freshness (E24).
- `EventsFrame.test.ts` — the Ocharo frame: bar, version badge, account menu with versions and no language list, English kept when another is asked, apps menu; the sidebar's
  places for an admin and a member, the open one following the route, the icon rail's stored choice, the phone's bottom bar with every
  place and no More; and "Sources updated" at the sidebar's foot, as its icon on the rail, as that icon beside the feed's title on a phone only
  (`md:hidden`), read once for both and not at all without `events:read`.
- `EventsRouter.test.ts` — `/` to the session organization's feed, the feed, calendar and reminders places, other organizations and missing
  permission as not found, and no Deadlines place.
- `OcharoAppsConfig.test.ts` — the Events entry of the apps menu and its runtime and build-time addresses.
- `TelegramDelivery.test.ts` — the digest editor's defaults (the digest named "Ocharo events", its content in Uzbek) and the mirrored digest routes; no "Deadline reminders" offer.
- `UiMessages.test.ts` — a phrase with no entry answers its English source (new strings need English only), existing entries keep a well-formed
  uz and ru text and their `{params}`, the kit's labels carry English, and no "Showcase events" string is left in the source.
- `UseUiLocale.test.ts` — the interface is English only (developer, 2026-11-20): a stored or browser language is ignored and left as it is, and a
  change to another language is a no-op that stores nothing.
- `helpers/` — the app mount and the router.

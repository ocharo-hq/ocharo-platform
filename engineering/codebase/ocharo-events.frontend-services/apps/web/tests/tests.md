# Tests

_Last updated: 2026-11-20_

Vitest unit and component tests over happy-dom; run `pnpm test` from the workspace root after `pnpm install`. No API, browser or
network is needed: each test stubs `fetch` (the session, status, runtime-settings and mirrored Events routes).

- `EventsPage.test.ts` — the feed on `fe-events`' kit (0.0.14, v2) through this app's mirror (OE07): the header as the title alone and the
  kit's own heading only for screen readers, one tracking request with skipped and rejected left out, the facet bar (the "n of N" pill, Ocharo's
  scope and kind chips and stages with the server's counts, one facets read per query change, no v1 `variant` or `shownFields`), the sort and the
  month dividers, scope chips and the hidden-stages choice sent server-side, titles by language, a quick Shortlist from the card's overflow menu,
  "Add event" (the country through the kit's picker), one event's tracking save, and no workflow write on a visit (any role, any stored
  workflow), Ocharo's labels filling what Events lacks; and the attention pills between the bar and the cards: the kit's deadlines pill and its
  list with links, the past pill with its count in all languages, a past event closed from the slot's strip, no pill at zero, both read again
  after a write in the feed. Scroll loading stays the kit's own test.
- `CalendarPage.test.ts` — the calendar place: the visible month read through the feed's calls and paged by cursor (capped at 1000),
  all-day and month-only events, stage colours, a new read for the next month and none inside the days read, loading, failure with retry,
  a block opening its event in the feed.
- `EventsCalendar.test.ts` — the calendar's model: the window each view reads, all-day blocks with an inclusive end, timed blocks in the
  event's zone shown in the browser's, events with no day and closed stages left out, titles by language, stage colours (theme variables only,
  never a hex; an untracked event never the muted surface).
- `EventsSections.test.ts` — the sections, their groups and addresses, and Telegram delivery for an admin only.
- `RemindersPage.test.ts` — the reminder settings' placeholder in all three languages.
- `SourcesFreshness.test.ts` — "Sources updated {time}" (E24) from the state the frame offers: fresh, stale past 24 hours (none at exactly 24),
  failing sources, none before the first record, nothing before a read or outside the frame, in all three languages, and compact as an icon with
  its tooltip and name.
- `IntakeFreshnessRead.test.ts` — the one freshness read: once on mount, again every five minutes and on return to the tab, nothing while there
  is no client, null when it fails, and stopped with its component.
- `UsePastToClose.test.ts` — "Past, not closed" (OE12): the query by stage and last start day, an ended confirmed event listed and a
  running or untracked one not, Attended or Skipped as one tracking write that keeps the rest, a conflict reloading the list, another failure
  kept with the list, and the count of events waiting.
- `PastEventsStrip.test.ts` — the strip (the attention pill's body) over what the hook holds: the ended events with their dates and stage, Attended
  and Skipped asking to close, a viewer offered no action, the changed notice, a failure with its reload, and an event opened in the page.
- `EventsModel.test.ts` — Ocharo's kit configuration (workflow, kinds, scopes, fields, proposal) and the page's addresses; the
  workflow spec equals `deployment/events/ocharo-workflow.json`, and what the page shows of a stored workflow; the 24-hour
  boundary of Events' intake freshness (E24).
- `EventsFrame.test.ts` — the Ocharo frame: bar, version badge, account menu with languages and versions, apps menu; the sidebar's
  places for an admin and a member, the open one following the route, the icon rail's stored choice, the phone's bottom bar with every
  place and no More; and "Sources updated" at the sidebar's foot, as its icon on the rail, as that icon beside the feed's title on a phone only
  (`md:hidden`), read once for both and not at all without `events:read`.
- `EventsRouter.test.ts` — `/` to the session organization's feed, the feed, calendar and reminders places, other organizations and missing
  permission as not found, and no Deadlines place.
- `OcharoAppsConfig.test.ts` — the Events entry of the apps menu and its runtime and build-time addresses.
- `TelegramDelivery.test.ts` — the digest editor's defaults and the mirrored digest routes; no "Deadline reminders" offer.
- `UiMessages.test.ts` — every `t(…)` literal and every label handed to the kit exists in the uz and ru maps, `{params}` survive, the
  SDK calendar's copy is translated, and no "Showcase events" string is left in the source.
- `helpers/` — the app mount and the router.

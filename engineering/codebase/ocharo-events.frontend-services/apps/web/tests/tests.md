# Tests

_Last updated: 2026-11-20_

Vitest unit and component tests over happy-dom; run `pnpm test` from the workspace root after `pnpm install`. No API, browser or
network is needed: each test stubs `fetch` (the session, status, runtime-settings and mirrored Events routes).

- `EventsPage.test.ts` — the page on `fe-events`' kit through this app's mirror (OE07): one tracking request with
  skipped and rejected left out, scope chips and the toggle sent server-side, the deadlines panel, titles by language,
  a quick Shortlist, "Add event", closing a past event from the strip, one event's tracking save, and no workflow write on a visit
  (any role, any stored workflow), Ocharo's labels filling what Events lacks; and the sources freshness line under the title
  (E24): fresh, stale past 24 hours (none at exactly 24), failing sources, none before the first record, nothing when the read
  fails, in all three languages.
- `PastEventsStrip.test.ts` — "Past, not closed" (OE12): the query by stage and last start day, an ended confirmed event listed and a
  running or untracked one not, Attended or Skipped as one tracking write that keeps the rest, a conflict reloading the list, a viewer
  offered no action.
- `EventsModel.test.ts` — Ocharo's kit configuration (workflow, kinds, scopes, fields, proposal) and the page's addresses; the
  workflow spec equals `deployment/events/ocharo-workflow.json`, and what the page shows of a stored workflow; the 24-hour
  boundary of Events' intake freshness (E24).
- `EventsFrame.test.ts` — the Ocharo frame: bar, version badge, no sidebar, account menu with languages and versions, apps menu.
- `EventsRouter.test.ts` — `/` to the session organization's events, other organizations and missing permission as not found.
- `OcharoAppsConfig.test.ts` — the Events entry of the apps menu and its runtime and build-time addresses.
- `TelegramDelivery.test.ts` — the digest editor's defaults, the reminder preset and the mirrored digest routes.
- `UiMessages.test.ts` — every `t(…)` literal and every label handed to the kit exists in the uz and ru maps, and `{params}` survive.
- `helpers/` — the app mount and the router.

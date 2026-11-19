# Tests

_Last updated: 2026-11-19_

Vitest unit and component tests over happy-dom; run `pnpm test` from the workspace root after `pnpm install`. No API, browser or
network is needed: each test stubs `fetch` (the session, status, runtime-settings and mirrored Events routes).

- `EventsPage.test.ts` — the page on `fe-events`' kit through this app's mirror (OE07): one tracking request with
  skipped and rejected left out, scope chips and the toggle sent server-side, the deadlines panel, titles by language,
  a quick Shortlist, "Add event", one event's tracking save, and the workflow aligned once for an admin.
- `EventsModel.test.ts` — Ocharo's kit configuration (workflow, kinds, scopes, fields, proposal) and the page's addresses.
- `EventsFrame.test.ts` — the Ocharo frame: bar, version badge, no sidebar, account menu with languages and versions, apps menu.
- `EventsRouter.test.ts` — `/` to the session organization's events, other organizations and missing permission as not found.
- `OcharoAppsConfig.test.ts` — the Events entry of the apps menu and its runtime and build-time addresses.
- `TelegramDelivery.test.ts` — the digest editor's defaults, the reminder preset and the mirrored digest routes.
- `UiMessages.test.ts` — every `t(…)` literal and every label handed to the kit exists in the uz and ru maps, and `{params}` survive.
- `helpers/` — the app mount and the router.

# Tests

_Last updated: 2026-11-10_

Vitest unit and component tests over happy-dom; run `pnpm test` from the workspace root after `pnpm install`. No API, browser or
network is needed: each test stubs `fetch` (the session, status, runtime-settings and events routes) or mocks the events endpoints.

- `EventsPage.test.ts`, `EventsModel.test.ts`, `EventCalendar.test.ts` — the page moved from Desk: feed, filters, detail, forms,
  the events model and addresses, the calendar export.
- `EventsFrame.test.ts` — the Ocharo frame: bar, version badge, no sidebar, account menu with languages and versions, apps menu.
- `EventsRouter.test.ts` — `/` to the session organization's events, other organizations and missing permission as not found.
- `OcharoAppsConfig.test.ts` — the Events entry of the apps menu and its runtime and build-time addresses.
- `UiMessages.test.ts` — every `t(…)` literal exists in the uz and ru maps, and `{params}` survive translation.
- `helpers/` — the app mount and the shared pickers.

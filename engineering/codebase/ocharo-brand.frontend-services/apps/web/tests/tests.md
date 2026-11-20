# Tests

_Last updated: 2026-11-20_

Vitest unit and component tests over happy-dom; run `pnpm test` from the workspace root after `pnpm install`. No API, browser or
network is needed: each test stubs `fetch` (the session, status and runtime-settings routes) or mocks Brand's endpoints.

- `BrandFrame.test.ts`, `BrandFrameUiMessages.test.ts` — the Ocharo frame: sidebar, phone bar, app menu, breadcrumb,
  not-found place and the English-only interface (a language change ignored, no language list, a phrase with no entry answering its English source).
- `UseUiLocale.test.ts` — a stored `uz` choice (`ocharo.operator.locale`) leaves the interface English and is not overwritten; a change to any other language is ignored.
- `BrandAccountMenu.test.ts` — the SDK BFF session: account menu, sign-in redirect and failed sign-in, sign-out, CSRF,
  expiry and refusals.
- `BrandQueryState.test.ts` — the query-backed brand store: first-read outcomes, refresh, the command lock, session
  lifetime and skeleton geometry.
- `BrandApi.test.ts` — Brand's endpoints over the SDK client and their safe failure copy.
- `BrandFirstPaint.test.ts` — `index.html` (English `lang`, title and splash label), the pre-paint colour script, the icons and the splash twin.
- `OcharoAppsConfig.test.ts` — the app menu's runtime and build-time addresses.
- `Architecture.test.ts` — the layer import rules.
- `helpers/` — the app and store mounts and the Brand fixtures they share.

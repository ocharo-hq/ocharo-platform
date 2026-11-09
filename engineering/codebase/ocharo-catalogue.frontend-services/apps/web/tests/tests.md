# Tests

_Last updated: 2026-11-09_

Vitest unit and component tests over happy-dom; run `pnpm test` from the workspace root after `pnpm install`. No API, browser or network is needed: `setup.ts` starts each test signed in as the owner with its own query client, and each test stubs `fetch` or mocks Catalogue's endpoints. `helpers/` holds the app mount, the session fixtures and the test transport; the browser suite lives in `../e2e/`.

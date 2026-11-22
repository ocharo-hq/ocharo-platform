# Dev

*Last updated: 2026-11-22*

Development-only helpers for the Vite dev server (`vite.config.ts`); nothing here ships in a build. `ProxyProblem.ts` answers a
structured 503 when the `/api` proxy cannot reach the API. The rest is **replay mode**: `pnpm dev:replay` serves the whole `/api`
surface from a snapshot of the dev API's real responses, so the layout can be worked on locally with real events, no backend and no
sign-in. It exists because live dev data cannot be reached from `localhost`: the dev Identity accepts only the dev callback and the
edge rejects a `localhost` Host. Filtering, sorting, facet counts and the summary are computed here over the snapshot, so a filter
changed in the UI behaves as it does against the service.

## Run

- Run `pnpm dev:replay` from the frontend root (or `pnpm dev:replay` in `apps/web/`): `VITE_REPLAY=1 vite`, the same `https://localhost:8292`.
- Needs the snapshot folder `.local/events-replay/` of the platform repository (ignored by Git; never copy or commit it) and fails at
  start with a message naming the missing file when one is absent. `replay.json` is `{"organizationId": "<the session's organization>"}`,
  written by the developer: the replay never invents an id.
- Restart after replacing a snapshot file; the folder is read once at start.
- `pnpm dev` and the production build are unchanged: the plugin and the proxy switch apply only to `vite` in serve mode with `VITE_REPLAY=1`.

## Snapshot

The snapshot is captured by hand from a signed-in dev tab of the Events app: from its console (same origin, so the session cookie rides)
fetch the mirrored routes and save each answer as it came. `tracking-all.json` is the tracking list for the whole catalog (undated events
and every stage included, paged by cursor into one `{"data": {"items": [...]}}`); `facets-0.json` and `facets-1.json` are the facets
answers of the feed's own query and its base query (the server reads `facets-0.json` only for the catalog's kinds and the workflow's
stages in order; the other is a shape reference); `summary.json` is the summary (a shape reference too, never served);
`workflow.json` is the workflow route's answer, a 404 problem body, which the replay answers again; `runtime-config.json` is
`/api/runtime-config` and `system-status.json` is `/api/system/status`. The capture is a manual step; a tool that writes it is not built.

## Served

- `GET /api/runtime-config`, `/api/system/status`: the snapshot's answers as recorded.
- `GET /api/identity/session`: a synthetic signed-in session (a "Replay Reviewer" with no real person data) of the organization in
  `replay.json`, with `events:read` and `events:track` (no `events:admin`, so the Telegram delivery place stays hidden), expiring in 2099.
  `GET /api/identity/sign-in?returnUrl=` redirects to a same-origin `returnUrl` at once.
- Under `/api/organizations/{orgId}/events-api/api/events/v1/` (another organization, another catalog: 404):
  `catalogs/{catalog}/tracking` (list, with `limit` and `cursor`), `tracking/facets`, `tracking/summary`, `tracking/{id}` and its
  `history` (always empty), `events/{id}`, the stored-image route `events/{id}/images/{index}` (302 to the image's own https `url`), and
  `workflow` (the recorded 404 problem).
- Every other GET answers a 404 problem; every non-GET (a tracking write, a quick Shortlist, "Add event", sign-out) answers 403
  `{"title": "Replay mode is read-only"}`, and the UI shows its failure notice.

## Simplifications

- Today is the dev machine's UTC day at each request (the summary takes its own `today` query), so the snapshot ages: events whose days
  have passed drop out of "upcoming". Refresh the snapshot when it is stale.
- The workflow is read from the facets file: the stage order, with the first stage as the default stage an untracked event reads as; the
  lost stages the summary's deadlines skip are Ocharo's `skipped` and `rejected`, set in `ReplayApi.ts`.
- A cursor is `replay.{sort}.{offset}`, not the service's keyset; a cursor of another sort or one that does not read is a 400, and
  `limit` is clamped to 1 to 200 (50 when absent).
- Titles order by the code units of the lowercased first title (a known language before `und`), not PostgreSQL's collation.
- Validation covers the sort, priority and status values, ISO-2 country codes, a stage both listed and left out, the summary's `today`
  and `deadlineKinds` and the cursor; the service's other limits (at most 50 values per list, key patterns) are not enforced.
- Not served (404): the calendar export (`events.ics`), the catalog's plain event list (the feed uses the tracking list), the digests of
  the Telegram delivery and the intake routes.

## Files

- `ReplayQuery.ts` — the pure query engine: `parseTrackingQuery`, `filterTracking`, `sortTracking`, `pageTracking`, `buildFacets`,
  `buildSummary`, after the service's own rules (a window keeps an event whose days overlap it, an untracked event reads as the default
  stage, a country list needs a known country, each facet counts without its own selection). Tested in `tests/ReplayQuery.test.ts`.
- `ReplayApi.ts` — the `replayApi({ dir })` Vite plugin: reads the snapshot, answers `/api` as a dev-server middleware.
- `ProxyProblem.ts` — the proxy's 503 problem.

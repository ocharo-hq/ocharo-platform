# Saved design revision history

> Preserved pre-consolidation evidence; not an active plan. Current work belongs to the canonical version track and backlog. Historical permissions and runtime observations are not current authorization.

Date: 2026-09-30

## Delivered

- Collapsed, lazy-loaded history for the saved design in its current organization.
- Descending revision metadata with paginated older rows and duplicate suppression during concurrent saves.
- Historical page selection using the same print preview renderer as the editor.
- Explicit `Restore as new unsaved copy` event carrying a cloned snapshot, title, and source revision.
- Viewers can inspect; editor or owner permission is required to restore a copy.
- Transient editor/session locks prevent restoration and new reads.
- Requests cancel on collapse, context change, invalidation, and disposal; stale results cannot replace newer selections.
- Failed/mismatched reads display an error and retain the active editor document.
- Existing authenticated read-only revision routes are reused; no backend contract or publication behavior changes.

## Integration contract

`DesignRevisionHistory.vue` receives `access`, `organizationId`, `document`, `canEdit`, and optional `disabled`.
Its `restore` event is `DesignRevisionCopy` from `domain/workspace/DesignRevisions.ts`.
The parent calls its existing document-store `create` operation, which owns dirty-state confirmation and produces an unsaved document.

## Verification

Existing Node 24.11.0 runtime; no dependency installation or runtime restart.

- `pnpm --filter @brand-workspace/web exec vitest run tests/DesignRevisionHistory.test.ts tests/DesignRevisionApi.test.ts`: 15 tests passed.
- `pnpm --filter @brand-workspace/web typecheck`: passed, including SFC macro compilation.
- ESLint scoped to the six assigned source/test files: passed.
- Tests cover lazy loading, read-only preview, explicit cloned restoration, viewer/lock checks, concurrent pagination overlap, stale responses, context switch, collapse, disposal, invalidation, retry, malformed responses, encoded organization routes, and unsafe snapshot rejection.

The parent lane owns editor integration and integrated browser verification. No login, publication, Git staging, or runtime changes were performed by this lane.

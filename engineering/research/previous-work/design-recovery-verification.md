# Design recovery verification

> Preserved pre-consolidation evidence; not an active plan. Current work belongs to the canonical version track and backlog. Historical permissions and runtime observations are not current authorization.

*Last updated: 2026-09-30*

## Behavior

The catalogue and business-card editors offer **Enable tab recovery** as an explicit opt-in.
Recovery uses `sessionStorage`, scoped by authenticated user ID, organization ID, and editor kind.
The integration implements the SDK `StorageBroker` contract with verified synchronous writes.
Independent browser tabs do not share a mutable recovery record.

- Unsaved title, validated design snapshot, source document ID, and source revision are retained.
- Capture completes synchronously when editor state changes, supporting reload and re-sign-in recovery.
- **Restore as new copy** creates an unsaved design with no saved document ID or expected revision.
- A previous pending recovery pauses automatic replacement until it is restored or discarded.
- **Discard tab recovery** removes the recovery record without changing the open design.
- **Stop keeping new changes** disables future capture while preserving an existing recovery for explicit review.
- A confirmed server save clears its matching recovery checkpoint.
- Undoing completely to the saved baseline clears the active recovery.
- A newer controller checkpoint is preserved when an older save completes.

The editor integration is `DesignRecovery.vue` with `recovery="store.recovery"` and the editor's
disabled state. `DocumentStore.create()` returns a success boolean; `load()` returns
`Promise<boolean>`. Editing and loading respect pending operations and authentication checks.

## Boundaries

- Storage is untouched until authenticated user and organization scope are available.
- Snapshot capture requires opt-in; session credentials and tokens are never copied from authentication state.
- Stored records are schema-validated; unrelated imported extension fields are removed.
- Uploaded images remain protected source references, not copied image bytes.
- Storage failures and quota exhaustion appear in the recovery panel.
- Records exceeding 1 MiB are rejected with guidance to save or download an editable file.
- No background server save, publication, source-document overwrite, or credential persistence is introduced.

## Executed checks

Recovery lane verification passed **24 tests across four files**:

- `DesignRecovery.test.ts`: opt-in, identity isolation, independent tab brokers, schema validation,
  size bounds, image references, pending records, quota failures, checkpoint ownership, and disposal.
- `DesignRecoveryStore.test.ts`: synchronous capture before invalidation, restore as a new copy,
  confirmed-save cleanup, undo cleanup, pending-source preservation, and mutation guards.
- `DesignRecoveryView.test.ts`: explicit controls, visible lifetime guidance, errors, and disabled actions.
- Existing `DesignStore.test.ts`: save confirmation, conflict preservation, copy saving, and undo/redo.

Owned-file ESLint checks passed. Final app-wide checks and browser integration verification belong
to the coordinating build lane; they are not inferred from these focused tests.

## Limits

Recovery supports this tab's reload and re-sign-in workflow. Closing the tab or browser can remove
it; **Save design** remains necessary for a durable server revision. Images still require access to
their source routes. Each account/organization/editor scope retains one recovery record in its tab.
A pending record must be resolved before new edits replace it. Recovery is not publication approval.

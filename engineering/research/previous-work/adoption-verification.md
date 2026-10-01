# Brand and Catalogue adoption verification

> Preserved pre-consolidation evidence; not an active plan. Current work belongs to the canonical version track and backlog. Historical permissions and runtime observations are not current authorization.

*Last updated: 2026-09-29*

## Result

Brand and Catalogue source adoption is verified at the new Platform paths. The existing Desk foundation and index were preserved. No runtime merge, database migration, authentication integration, staging, commit, publication or process restart was performed.

## Executed checks

| Check | Result |
|---|---|
| Brand backend solution, `dotnet test ... -p:BuildSpa=false` | 5 passed, 0 failed, 0 skipped |
| Catalogue backend solution, `dotnet test ... -p:BuildSpa=false` | 18 passed, 0 failed, 0 skipped |
| Brand frontend `pnpm test` | 4 passed |
| Catalogue frontend `pnpm test` | 12 passed |
| Brand frontend `pnpm build` | Vue/TypeScript/SFC checks and Vite production build passed |
| Catalogue frontend `pnpm build` | Vue/TypeScript/SFC checks and Vite production build passed |
| Authored source/config/lockfile checksum comparison | All imported code files match donor working-tree bytes |
| Runtime-data checksum comparison | 328 copied files match donor snapshots after testing |
| Git ignore check | All 328 runtime-data files ignored |
| Adopted Markdown link check | No broken relative links |
| Platform index hash | Unchanged from the pre-import snapshot |
| Nested Git metadata | No `.git` imported under companion code roots |

Backend commands ran from the Platform root with normal package restore; API tests use isolated temporary data. Frontend dependencies were restored from the existing pnpm cache with `--offline --frozen-lockfile` (252 packages per workspace, no downloads). Frontend tests/builds ran with Node 24.11.0 and pnpm 10.33.2. The offline install initially used the shell's Node 20 and emitted an engine warning; all executed frontend verification used the required Node 24. No lockfiles changed. The existing `vue-demi` lifecycle script remained disabled by pnpm; builds passed without changing that setting.

## Preserved source and data

- Brand: 97 authored files plus 7 runtime-data files.
- Catalogue: 134 authored files plus 321 runtime-data files.
- Eight copied documentation/deployment files per domain had relative links or build references adapted.
- Imported implementation, package definitions, lockfiles, API configuration and runtime stores are unchanged.
- Donor CI and agent configuration were not activated. Their original checkouts remain under workspace archival ownership.
- Per-file original/destination checksums are recorded by the workspace consolidation evidence in `ocharo-platform-adoption-map.json`.

## Verification boundary

This verifies source relocation and companion build/test behavior. It does not establish browser review, container-image verification, deployment, live sibling contracts, provider integrations, authentication convergence or database convergence. No companion runtime was launched. The existing Desk API was observed through its compatibility path and left running.

The source and commands are documented in the [adoption guide](../adoption/adoption.md).

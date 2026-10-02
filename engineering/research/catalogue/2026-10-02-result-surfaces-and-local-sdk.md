# Catalogue result surfaces and local SDK builds

*Last updated: 2026-10-02*

## Result layout contract

A collection region keeps its page header, search/filter controls and refresh action across loading, content, successful empty and unavailable outcomes. Refresh is a persistent toolbar action. Successful empty responses show a plain centered label within the reserved result region. A failed initial request uses distinct neutral unavailable copy; request diagnostics remain in localized top-right notifications. A failed refresh retains the last successful projection.

Stock, Copy and Prices use row/cell skeletons inside their tables. Product, Collection and Catalogue grids use their corresponding geometry. Existing names, identities and controls remain mounted during refresh; only changing values use the shared SDK skeleton slots. Known empty regions remain empty while refreshing. Shared loading uses the SDK 400 ms minimum, opacity transitions and reduced-motion fallbacks. Local discard confirmation precedes refresh; cancellation preserves drafts and does not start skeletons. Writes cannot race a pending refresh.

The native Catalogue uses the genuine published UI SDK `0.0.12` and the required `@tanstack/vue-query` peer `5.102.8`. Unpublished notification APIs are not added to its published dependency manifest.

## Local backend package integration

Seven real backend solutions build with a verified ignored local package overlay, including the normal Catalogue command without SDK arguments:

```sh
dotnet build OcharoCatalogue.BackendServices.slnx -m:1
```

The local package family is `10.0.63-local.11d3cf6b7566`, built from a hashed source snapshot. It has a separate package cache, package-source configuration and per-project ignored lockfiles. Public package pins, checked-in lockfiles and the global package cache remain intact. The SDK Web tests and seven-package verification passed before installation. Catalogue passed 55 tests, including four HTTP ProblemDetails boundary tests; the six sibling apps passed 40 scoped tests. All seven ordinary builds passed.

The portable bootstrap and opt-in imports are committed independently from API source requiring the unpublished methods. [Local SDK preparation](https://github.com/ocharo-hq/ocharo-ws/blob/main/docs/local-sdk-verification.md) documents setup, safe retry and removal. A clean setup-only Catalogue snapshot builds against its genuine published `10.0.59-beta` pins. Released-package adoption of the new HTTP boundary remains pending genuine SDK publication.

Motion's combined backend/frontend build uses `pnpm run deploy`; generated `wwwroot` output remains on disk and is excluded from Git. The tracked placeholder is removed from the index, without deleting the generated local application.

## Canonical rules

The WoW2 owners `emptyState.md`, `state-and-data.md`, `toastHost.md` and `notificationCenterGroup.md` record plain result labels, persistent toolbar refresh, matched loading geometry and explicit notification retention. Planning tasks `con-t-004` through `con-t-008` retain the remaining non-Ocharo controller, HTTP, request-feedback, confirmed-write and notification adoption work. Convention changes remain local alongside another chat's broader convention migration.

## SDK notification verification

The 19-file frontend SDK batch is staged and remains unpublished. Local unit/SSR/DOM, lint, format, types, SFC compilation, build and packed-consumer checks passed. Close and toast expiry preserve retained history; Delete is available only for retained notices and removes their history entry. Retained data is scoped to an explicit bus; storage/hydration is caller-owned. All toast hosts use top-right placement with severity icons. The new notification center is being validated in an isolated Catalogue consumer before actual adoption.

The complete SDK browser gate is not green: an unrelated `ImageCropEditor` Chromium import fails, and Firefox's native sandbox/graphics launch fails locally. The owning lane did not edit unrelated SDK source. These failures remain release gates; local build results are not a publication claim.

## Owner review

Review the native Catalogue app at `https://localhost:8282/catalogues`; Desk is a separate app. Rebuild in the existing IDE sessions to review these source changes.

1. Use Refresh in the toolbar. A successful empty response should show only a centered label.
2. Review Collections, Products, Stock, Copy and Prices. Table pages should skeletonize their rows, not insert image cards.
3. Edit a stock/copy draft and cancel Refresh's discard modal. The draft and visible content should remain intact.
4. Create a named catalogue, selecting Web, PDF or both. Open its available designers from its gallery card.
5. Review `/catalogues/{id}/web` with desktop/tablet/mobile previews and `/catalogues/{id}/pdf` with millimetre pages.

Human visual acceptance remains open. No user-owned service or browser was restarted or operated, and no live product database or customer publication was changed.

## Verification receipts

The final native Catalogue gate passed 136 tests across 18 files, lint, formatting, typechecks and a production build including 32 SFCs. Focused lifecycle and geometry coverage passed 38 tests across two files. Checks cover first failure versus successful emptiness, matched table/form/designer structure, retained row identities and values, canceled draft refresh and disabled writes during refresh. The build retains the existing advisory chunk-size warning; no gate or assertion was weakened.

Actual backend package builds and 95 scoped app tests passed as described above. Marketing CI `37004156763` at `c9b8317` and Assets CI `37004482939` at `8969df0` passed. Studio CI `37004163595` at `c3f3d8c` exposed two cold-start browser acknowledgement failures; investigation is active. Platform's earlier notification placement CI `37002585088` at `d0f14dd` passed; the result-layout batch's own CI is pending push.

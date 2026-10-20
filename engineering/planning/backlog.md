# Ocharo Platform — Backlog

*Last updated: 2026-10-20*

## Features

Build order set by the owner on 2026-10-08: **Events → Sourcing → Catalogue**. Later slices of a vector wait until the vectors before it are confirmed.

| Feature | State | Spec |
|---|---|---|
| Workspace and approvals | implemented; owner review open | [preparation.md](../../product/features/preparation.md) |
| Catalogue editions | implemented; owner review open | [catalogue.md](../../product/features/catalogue.md) |
| Image processing | implemented; owner review open | [image-processing.md](../../product/features/image-processing.md) |
| Sourcing: suppliers, contacts, shared media | in progress — v0.1 iteration 16 | [sourcing.md](../../product/features/sourcing.md) |
| Events: showcase events feed | in progress — v0.1 iteration 17 | [events.md](../../product/features/events.md) |

## Product and delivery

| Item | Type | Notes |
|---|---|---|
| Provider delivery | feature | Connect one authorized provider with durable send receipts. |
| Publishing recovery | engineering | Verify restart, lease expiry and crash-after-send reconciliation. |
| Metrics observations | feature | Distinguish unknown, stale, unsupported and measured zero. |
| Campaign recurrence | feature | Explicit schedules, channel formats and review collaboration. |
| Brand vector masters | feature | Sanitized source SVG and exported brand guidelines. |
| Merchant evidence | feature | Validate order evidence and real workflow attribution. |
| Companion consolidation | engineering | Retained Brand/Catalogue pilots stay loopback-only until absorption. |
| Hosted rollout | engineering | Verify backups, keys, provider configuration and target approval. |
| Migration version adoption | engineering | Adopt a published SDK migration field supporting full prerelease versions beyond 20 characters; never truncate identity. |
| SDK startup migration seam | engineering | `MigrateBespokeOnStartupAsync` needs `AddPostgresPersistence` and creates the database; Desk owns its `NpgsqlDataSource`, so its host calls `IMigrationRunnerService` directly and installs the Hangfire schema itself. Adopt a published helper over a caller's own data source when the SDK offers one. |

## Catalogue review — 2026-10-01

The active review target is native Catalogue `https://localhost:8282`, backed by
`OcharoCatalogue.Api` HTTPS `8278`. The earlier Desk-only implementation did not meet the
requested app identity. Native changes retain Catalogue's existing products and revision history.
See [native verification](../research/catalogue/2026-10-01-catalogue-review/native-catalogue-verification.md)
and [owner review scenarios](version-track/v0.1/v0.1.md#iteration-6--catalogue-app-integration).

### Implemented; owner review open

- Approved Ocharo wordmark with Catalogue subtitle and top navigation.
- Preferences contains language, light/dark/system theme and build information.
- Products contains translation review, UZS 1–9/10+ pricing and active/grouped media editing.
- Language approval issues appear on cards; collections use capped membership pills.
- Final active image validation, SDK dropdowns and blurred product/collection modals.
- Full-screen product/collection editing on smaller screens, preserving unsaved changes.
- Named catalogue gallery with separate responsive Web and exact-page PDF designers.
- Whole PDF, current page PDF/PNG, original design template and product-bound images/copy.
- Explicit preparation, revision/fingerprint checks, immutable publication and withdrawal.
- Public named reader with local guest saved pieces and released PDF/page downloads.
- Toast failures, skeleton content loading and in-app warning confirmations.

### Remaining integration

- [ ] Add authenticated Catalogue sessions and server-enforced admin/manager write permissions.
      Loopback-only Catalogue writes must not be exposed publicly before this exists.
- [ ] Make the signed-in logo open the workspace/app launcher; keep public navigation role-aware.
- [ ] Reconcile Catalogue and authenticated Desk product/edition ownership through an explicit migration.
      No automatic cross-store writes or synchronization were added.
- [ ] Synchronize signed-in saved products; guest saving currently stays in the browser.
- [ ] Reuse the reviewed application lockup in Studio and other appropriate Ocharo applications.
- [ ] Review additional-language typography and authored translations; the 25-language registry
      does not imply translated product content or owner approval.

The public reader consumes explicitly published releases. Draft edits do not publish themselves.
Archiving a native catalogue or explicitly withdrawing it removes its public availability while
retaining stored history; re-publication requires fresh preparation.

## Desk sourcing and event integrations — 2026-10-09

Design and sources: [analysis](../research/desk/2026-10-09-sourcing-events-integrations.md).

- [ ] Add material requests, supplier offers, quotation evidence and sample review before price comparison.
- [ ] Add event editions, source observations and reviewable import candidates without overwriting confirmed facts.
- [ ] Add one verified feed/provider adapter with provenance, cancellation handling and bounded requests.
- [ ] Add client authorization/refresh for unattended MCP; current access lasts five minutes.
- [ ] Add calendar OAuth only after calendar selection and outbound synchronization rules are agreed.
- [ ] Adopt SDK refresh timing in Desk; the current 0.0.6 package lacks the required orchestration exports.

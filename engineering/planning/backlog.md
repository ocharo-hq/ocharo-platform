# Ocharo Platform — Backlog

*Last updated: 2026-11-08*

## Features

Build order set by the owner on 2026-10-08: **Events → Sourcing → Catalogue**. Later slices of a vector wait until the vectors before it are confirmed.
Owner focus from 2026-10-24: **Events and Sourcing only**, then WoW2 File Storage and Identity adoption; the dev follow-ups below wait.

| Feature | State | Spec |
|---|---|---|
| Workspace and approvals | implemented; owner review open | [preparation.md](../../product/features/preparation.md) |
| Catalogue editions | implemented; owner review open | [catalogue.md](../../product/features/catalogue.md) |
| Image processing | implemented; owner review open | [image-processing.md](../../product/features/image-processing.md) |
| Sourcing: suppliers, contacts, shared media | in progress — v0.1 iteration 16 | [sourcing.md](../../product/features/sourcing.md) |
| Events: showcase events feed | in progress — v0.1 iteration 17; moving into the WoW2 Events SDK module (2026-10-24) | [events.md](../../product/features/events.md) |

## Product and delivery

| Item | Type | Notes |
|---|---|---|
| Provider delivery | feature | Connect one authorized provider with durable send receipts. |
| Publishing recovery | engineering | Verify restart, lease expiry and crash-after-send reconciliation. |
| Metrics observations | feature | Distinguish unknown, stale, unsupported and measured zero. |
| Campaign recurrence | feature | Explicit schedules, channel formats and review collaboration. |
| Brand vector masters | feature | Sanitized source SVG and exported brand guidelines. |
| Merchant evidence | feature | Validate order evidence and real workflow attribution. |
| Companion consolidation | engineering | Retained Brand/Catalogue pilots sign in through Ocharo's Identity since 2026-11-03 (G4); consolidating their stores with Desk stays open. |
| Pilot sign-in follow-ups | engineering | After G4 (2026-11-03): Catalogue's Playwright suite (`e2e/support/start-api.mjs`) has no `Identity:Client`, `Catalogue:OrganizationId` or signed-in session, so it no longer starts or seeds the API; each pilot's `deployment/{brand,catalogue}/docker-compose.yml` no longer boots without those settings; both keep BFF sessions in memory, so a restart signs everyone out (a durable SQLite or shared cache would end that). |
| Hosted rollout | engineering | Verify backups, keys, provider configuration and target approval. |
| Identity cutover | engineering | Ocharo people sign in by Telegram through Ocharo's WoW2 Identity, local → dev → prod, then every app; track: [identity-cutover.md](identity-cutover.md). |
| Own-phone check | verification | Verify Maftuna adds her own phone through the bot's add-phone link, then signs in on her own phone, not through her trusted contact; deferred by the owner on 2026-11-03 (identity-cutover.md #46). |
| Dev sign-in checks | verification | Deferred by the owner on 2026-11-04 (identity-cutover.md #49): read Desk's `GET /api/identity/session` on dev for Ocharo and `desk:*`, run an invite on dev, and complete a sign-in on Marketing, Editorial and Studio, which needs the owner's phone. Also parked by the owner on 2026-11-08 (identity-cutover.md #53, #54): a new browser's wait for a trusted contact's press in Telegram, which needs the owner's phone and a contact, and an own-phone change in a browser, which needs a second number. |
| Dev and prod follow-ups | engineering | Deferred by the owner on 2026-10-24: Marketing's card preview answers 500 on dev; Motion's private site gets no route; dev Desk has no operator yet; prod has never been deployed. |
| Desk's retired tables | engineering | Every app signs in through Ocharo's Identity and tickets are gone (2026-11-05), but Desk's `access.sessions`, `sso_*`, `telegram_*`, the six EF `identity_*` tables and `memberships` remain. Dropping them is the unwritten forward-only migration `011-identity-cutover`, which also needs `FoundationRepository`'s `access.sessions` check and the import CLIs' `UserManager` actor lookup changed first. |
| Ocharo File Storage adoption | engineering | Move Brand and Catalogue media, then Blog, Marketing and Studio, then Desk's `bytea` files, then Assets releases onto WoW2 Files: tenant `ocharo` on the shared files host B under Ocharo's own hostname, one bucket per environment, moving to Ocharo's own instance later by a tenant move (Files spec, 2026-10-26; `files-integration #30`). Media, order, delivery and callers: [files-adoption.md](files-adoption.md) #1–#16 (`files-integration #27`). Waits on Files' `Client` package and B's dev deployment (files-adoption #17). |
| Files registrations | engineering | Ocharo's Identity registry stays in this repo's `deploy.yml`: this repo adds resource `files` (`files.read`, `files.write`) and each app client's `client_credentials`, `token_exchange`, `Resources` and `ExchangeResources`; Wheelhouse types the per-environment `Uri`, `SignalUrl` and `Files:BaseUrl` (`files-integration #32`). The entries land with B's dev hostname, since a required untyped key refuses every Identity deploy: [files-adoption.md](files-adoption.md) #18–#19. |
| Migration version adoption | engineering | Adopt a published SDK migration field supporting full prerelease versions beyond 20 characters; never truncate identity. |
| SDK startup migration seam | engineering | `MigrateBespokeOnStartupAsync` needs `AddPostgresPersistence` and creates the database; Desk owns its `NpgsqlDataSource`, so its host calls `IMigrationRunnerService` directly and installs the Hangfire schema itself. Adopt a published helper over a caller's own data source when the SDK offers one. |
| Brand localization | feature | Localize the Brand pilot's pages (uz/ru/en): about 150–200 Uzbek literals across the five pages, skeletons, store notices, API failure copy and logo slot labels; replace `UzDates` with locale-aware dates; then read `ocharo.operator.locale` and offer the three languages in its account menu. |

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

- [x] Add authenticated Catalogue sessions and server-enforced admin/manager write permissions.
      Ocharo's Identity since 2026-11-03: editors write with `catalogue:edit`, owners and admins also publish with
      `catalogue:approve`; anonymous writes ended.
- [x] Make the signed-in logo open the workspace/app launcher; keep public navigation role-aware.
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
- [ ] Revoke and replace `@ocharo_dev_bot`'s token in BotFather, then update it in Wheelhouse's dev vault: it was briefly
  pasted into a Wheelhouse secret note, since cleared (owner, 2026-11-02).
- [ ] [parked] Desk MCP on Identity agent tokens: an MCP client gets its own Identity OAuth token, not Desk's browser
  session, and MCP stays off until then (owner, 2026-11-02). The ticket-authenticated surface left with the tickets on
  2026-11-05; its read tools, UI panel and tests are in commit `ce3ad7b` to restore from.
- [ ] Add calendar OAuth only after calendar selection and outbound synchronization rules are agreed.
- [ ] Adopt SDK refresh timing in Desk; the current 0.0.6 package lacks the required orchestration exports.

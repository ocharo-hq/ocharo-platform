# Ocharo Platform — Backlog

*Last updated: 2026-11-09*

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
| Ocharo File Storage adoption | engineering | Move Brand and Catalogue media, then Blog, Marketing and Studio, then Desk's `bytea` files, then Assets releases onto WoW2 Files: tenant `ocharo` on the shared files host B under Ocharo's own hostname, one bucket per environment, moving to Ocharo's own instance later by a tenant move (Files spec, 2026-10-26; `files-integration #30`). Media, order, delivery and callers: [files-adoption.md](files-adoption.md) #1–#16 (`files-integration #27`). Wave 1 (Brand, Catalogue's images and release artifacts) is built on Files and read back on dev (files-adoption #23–#26). |
| Files registrations | engineering | Ocharo's Identity registry stays in this repo's `deploy.yml` (`files-integration #32`). Written 2026-11-08: resource `files` (`files.read`, `files.write`, `files.share`, `files.admin`), each app client's `client_credentials`, `token_exchange`, `Resources` and `ExchangeResources`, and `files:*` permissions for owner, admin and editor. Open: Wheelhouse types `Resources:2:Uri` (dev: `https://ocharo-files-dev.{products host}.sslip.io/`) and `Resources:2:SignalUrl` (`{Uri}signals/identity`) before the first dev deploy that carries them, then each app's Files base URL with its adoption: [files-adoption.md](files-adoption.md) #18–#19. |
| Migration version adoption | engineering | Adopt a published SDK migration field supporting full prerelease versions beyond 20 characters; never truncate identity. |
| SDK startup migration seam | engineering | `MigrateBespokeOnStartupAsync` needs `AddPostgresPersistence` and creates the database; Desk owns its `NpgsqlDataSource`, so its host calls `IMigrationRunnerService` directly and installs the Hangfire schema itself. Adopt a published helper over a caller's own data source when the SDK offers one. |
| Brand localization | feature | Localize the Brand pilot's pages (uz/ru/en): about 150–200 Uzbek literals across the five pages, skeletons, store notices, API failure copy and logo slot labels; replace `UzDates` with locale-aware dates; then read `ocharo.operator.locale` and offer the three languages in its account menu. |
| PostgreSQL for Brand and Catalogue | engineering | Brand and Catalogue still run SQLite with `EnsureCreated`; move to PostgreSQL with a bespoke SQL migration set, keeping IDs and media hashes. Changes data; BFF sessions then persist. Size M2 L4; 4/6 change behaviour or data. Covers B-7–B-9, C-7–C-9 ([sweep](../research/convention-sweep.md)). Decided 2026-11-09 (developer): one `ocharo_{env}` database, shared with Desk and Identity, with schemas `brand` and `catalogue`; bespoke SQL migrations; FKs to Identity's organization and user ids; the SQLite data copied, dev first, the tool reused for prod; BFF sessions and Data Protection keys in PostgreSQL, which ends sign-out on restart. |
| SDK host composition | engineering | Desk, Brand and Catalogue hand-wire hosts, health, SPA serving and key rings; boot through `ServiceHost.RunAsync` and the API defaults bundle. One fix serves all three; no behaviour change. Size S12 M4 L1; 0/17 change behaviour or data. Covers D-15, D-17–D-20, B-1, B-2, B-4–B-6, B-16, C-1, C-2, C-4–C-6, C-17 ([sweep](../research/convention-sweep.md)). |
| Error shape and SDK JSON | engineering | `AddControllersWithSdkJson` and `AppResult` problem details replace hand-built errors in Desk, Brand and Catalogue. Changes the wire format, so frontend models retype and `vue-tsc` follows. Size S3 M2; 5/5 change behaviour or data. Covers D-21, B-3, B-14, C-3, C-15 ([sweep](../research/convention-sweep.md)). |
| Mediator, controllers and AppResult | engineering | Desk, Brand and Catalogue keep service logic in Application, throw exceptions, and Desk uses minimal-API routes; move to handlers returning `AppResult` behind `ISender` controllers. Two L items; no behaviour change. Size S3 M4 L4; 0/11 change behaviour or data. Covers D-22, D-23, D-25, D-26, B-11–B-13, C-11–C-14 ([sweep](../research/convention-sweep.md)). |
| Dapper data access | engineering | Desk's 11 repositories use raw `NpgsqlCommand` (~100 sites); move to Dapper over `IDbConnectionFactory`. Large, no behaviour change. Size L1; 0/1 change behaviour or data. Covers D-24 ([sweep](../research/convention-sweep.md)). |
| Desk migrations: retired tables and version stamp | engineering | Desk: write `011-identity-cutover` to drop retired identity tables and `AddIdentityCore`; adopt the SDK migration version field so full versions stop being truncated. Changes data. Overlaps two rows above. Size S1 L1; 2/2 change behaviour or data. Covers D-31, D-32 ([sweep](../research/convention-sweep.md)). |
| Deploy descriptor, env files and image versions | engineering | Descriptor gaps across Desk, Brand and Catalogue: missing `name`/`description`, `migrate` and `configCheck` items, `appsettings.Development.json`, `APP_VERSION` build args, a misplaced Dockerfile, an off-ledger compose port. Mostly small; two change behaviour. Size S7 M2; 3/9 change behaviour or data. Covers D-1, D-13, D-14, D-16, B-10, B-21, C-10, C-19, C-25 ([sweep](../research/convention-sweep.md)). |
| Backend code style, docs and time | engineering | One type per file, 120-character lines, type docs, `TimeProvider` instead of `IClock`, configuration bound to options. Desk, Brand and Catalogue; formatter-led and mechanical; no behaviour change. Size S5 M4; 0/9 change behaviour or data. Covers D-27–D-29, B-15, B-19, C-16, C-18, C-22, C-23 ([sweep](../research/convention-sweep.md)). |
| Tests: AwesomeAssertions and naming | engineering | Split the single Brand and Catalogue test projects into Unit, Integration and E2E tiers; rename to `{Unit}_Should…_When…`; move to AwesomeAssertions and `FakeTimeProvider`. Desk's 52 names too. No behaviour change. Size M5; 0/5 change behaviour or data. Covers D-30, B-17, B-18, C-20, C-21 ([sweep](../research/convention-sweep.md)). |
| SDK frontend controls and auth | engineering | Brand and Catalogue use hand-styled button and form utilities and an app-local BFF session client; adopt SDK `Button`, form controls and `createBffStrategy` after the SDK re-pin. Catalogue is L; changes UI behaviour. Size M3 L1; 2/4 change behaviour or data. Covers B-24, B-25, C-28, C-29 ([sweep](../research/convention-sweep.md)). |
| Frontend data access, state and storage | engineering | Raw `fetch`, thrown errors, module-level refs and direct `localStorage` use across Desk, Brand and Catalogue; move to `createApiClient`, `Result`, the query capability and the storage broker. No behaviour change. Size S5 M3; 0/8 change behaviour or data. Covers D-38, D-39, B-27, B-28, C-30–C-33 ([sweep](../research/convention-sweep.md)). |
| Frontend slice structure | engineering | Desk's single `workspace` slice (L) needs domain slicing, `common/` naming, declared routes and layout; Brand needs a not-found route. Moves files; Brand's route changes behaviour. Size S2 M1 L1; 1/4 change behaviour or data. Covers D-33–D-35, B-26 ([sweep](../research/convention-sweep.md)). |
| Frontend document, icons, locale and dev server | engineering | Favicons and touch icons, boot splash, Lucide imports, Uzbek date formatting and the `VITE_HTTPS` fallback across Desk, Brand and Catalogue. Small; most change visible behaviour. Size S7 M1; 7/8 change behaviour or data. Covers D-2, D-36, D-37, D-40, B-22, B-23, C-26, C-27 ([sweep](../research/convention-sweep.md)). |
| Docs and version-track hygiene | engineering | Missing `{folder}.md` leads, a `README` and `AGENTS.md`, two extra planning files, an overlong `v0.1` and backlog format. Desk, Brand and Catalogue; docs only; closing `v0.1` is L. Size S5 M6 L1; 0/12 change behaviour or data. Covers D-3–D-12, B-20, C-24 ([sweep](../research/convention-sweep.md)). |

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

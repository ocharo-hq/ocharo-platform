# Ocharo Platform — Backlog

*Last updated: 2026-11-10*

## Features

Build order set by the owner on 2026-10-08: **Events → Sourcing → Catalogue**. Later slices of a vector wait until the vectors before it are confirmed.
Owner focus from 2026-10-24: **Events and Sourcing only**, then WoW2 File Storage and Identity adoption; the dev follow-ups below wait.

| Feature | State | Spec |
|---|---|---|
| Workspace and approvals | shipped v0.1 | [preparation.md](../../product/features/preparation.md) |
| Catalogue editions | shipped v0.1 | [catalogue.md](../../product/features/catalogue.md) |
| Image processing | shipped v0.1 | [image-processing.md](../../product/features/image-processing.md) |
| Sourcing: suppliers, contacts, shared media | shipped v0.1 | [sourcing.md](../../product/features/sourcing.md) |
| Events: showcase events feed | shipped v0.1, on WoW2 Events since v0.3 | [events.md](../../product/features/events.md); served from WoW2 Events since 2026-11-10 ([events-adoption.md](../research/events-adoption.md)) |

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
| Pilot sign-in follow-ups | engineering | After G4 (2026-11-03): Catalogue's Playwright suite (`e2e/support/start-api.mjs`) has no `Identity:Client`, `Catalogue:OrganizationId` or signed-in session, so it no longer starts or seeds the API; each pilot's stand-alone `deployment/{brand,catalogue}/docker-compose.yml`, which no longer booted without those settings, was deleted on 2026-11-09; both keep BFF sessions in memory, so a restart signs everyone out (a durable SQLite or shared cache would end that). |
| Events on WoW2 Events | engineering | Done 2026-11-10 (v0.3, `events-integration #26`, `#27`): Events owns the catalog and tracking, Desk its page, membership and references (E1). Desk's page reads WoW2 Events through Desk's routes once `Events:BaseUrl` is set; a new event is a member proposal, stage, priority and notes are tracking, fact edits and deletes answer 410. The 68 local rows were exported with `export events`, imported by Events' operator verb on dev and reconciled 68 of 68 ([events-adoption.md](../research/events-adoption.md) #6–#8). Desk's old events store retired the same day (v0.3 Iteration 5, owner: "yes drop completely"): no rollback to it remains. |
| Shared app shell | engineering | Ocharo's seven operator apps adopt the one shell the fe sdk lane builds in `@wow-two-beta/fe` (`events-integration #59`, `#60`), so the frame matches across apps. Inventory of what each local shell does beyond the frame, what the shell must accept for it and the adoption order: [shell-adoption.md](../research/shell-adoption.md) #1–#5. Waits for the shell's published version and migration note. |
| Events registrations | engineering | Ocharo's Identity registry stays in this repo's `deploy.yml` (`events-integration #21`, `#22`, `#23`, `#30`; events-adoption.md #2–#5). Written 2026-11-10: resource 3 `events` (5 scopes, namespace `events`, signal URL), resource 4 `harvest` (11 scopes, no namespace, no signal URL), client 8 `events` (`harvest.events-read`), client 9 `harvest-operator` (removed 2026-11-10, no caller; index 9 left as a gap), client 10 `events-console` (code flow, namespace `events`), Desk's client 0 exchanging to `events` (`events.read`, `events.track`, `events.import`), and `events:*` on owner and admin (all four), editor (read, track), viewer (read). Secrets generate. Wheelhouse typed resources 3 and 4's URIs and resource 3's signal URL on dev before the push (2026-11-10); client 10's redirect, sign-out and signal URIs followed, and client 10 with them. |
| Hosted rollout | engineering | Verify backups, keys, provider configuration and target approval. |
| Identity cutover | engineering | Ocharo people sign in by Telegram through Ocharo's WoW2 Identity, local → dev → prod, then every app; track: [identity-cutover.md](../research/identity-cutover.md). |
| Own-phone check | verification | Verify Maftuna adds her own phone through the bot's add-phone link, then signs in on her own phone, not through her trusted contact; deferred by the owner on 2026-11-03 (identity-cutover.md #46). |
| Dev sign-in checks | verification | Deferred by the owner on 2026-11-04 (identity-cutover.md #49): read Desk's `GET /api/identity/session` on dev for Ocharo and `desk:*`, run an invite on dev, and complete a sign-in on Marketing, Editorial and Studio, which needs the owner's phone. Also parked by the owner on 2026-11-08 (identity-cutover.md #53, #54): a new browser's wait for a trusted contact's press in Telegram, which needs the owner's phone and a contact, and an own-phone change in a browser, which needs a second number. |
| Dev and prod follow-ups | engineering | Deferred by the owner on 2026-10-24: Marketing's card preview answers 500 on dev; Motion's private site gets no route; dev Desk has no operator yet; prod has never been deployed. |
| Ocharo File Storage adoption | engineering | Move Brand and Catalogue media, then Blog, Marketing and Studio, then Desk's `bytea` files, then Assets releases onto WoW2 Files: tenant `ocharo` on the shared files host B under Ocharo's own hostname, one bucket per environment, moving to Ocharo's own instance later by a tenant move (Files spec, 2026-10-26; `files-integration #30`). Media, order, delivery and callers: [files-adoption.md](../research/files-adoption.md) #1–#16 (`files-integration #27`). Wave 1 (Brand, Catalogue's images and release artifacts) is built on Files and read back on dev (files-adoption #23–#26). |
| Files registrations | engineering | Ocharo's Identity registry stays in this repo's `deploy.yml` (`files-integration #32`). Written 2026-11-08: resource `files` (`files.read`, `files.write`, `files.share`, `files.admin`), each app client's `client_credentials`, `token_exchange`, `Resources` and `ExchangeResources`, and `files:*` permissions for owner, admin and editor. Done: Wheelhouse typed `Resources:2:Uri` and `Resources:2:SignalUrl` on dev, and every app links its Files base URL to that Uri ([files-adoption.md](../research/files-adoption.md) #18–#19). |
| SDK startup migration seam | engineering | `MigrateBespokeOnStartupAsync` needs `AddPostgresPersistence` and creates the database; Desk owns its `NpgsqlDataSource`, so its host calls `IMigrationRunnerService` directly and installs the Hangfire schema itself. Adopt a published helper over a caller's own data source when the SDK offers one. |
| Brand localization | feature | Localize the Brand pilot's pages (uz/ru/en): about 150–200 Uzbek literals across the five pages, skeletons, store notices, API failure copy and logo slot labels; replace `UzDates` with locale-aware dates; then read `ocharo.operator.locale` and offer the three languages in its account menu. |
| Retire the SQLite volumes | engineering | Once `import-sqlite` has run on dev and prod for Brand and Catalogue, drop the `brand-data` and `catalogue-data` volumes' retired SQLite stores and file key rings, the `LegacySqlite` settings, `BrandStorageMovingMiddleware` and its Catalogue twin, the `import-sqlite` verbs and `Microsoft.Data.Sqlite`; blobs stored before WoW2 Files stay until their migration (Studio's row, `ocharo-studio` backlog). |
| Sweep exceptions | engineering | v0.2 kept five convention rows in part, each with what lifts it ([convention-sweep.md § Exceptions](../research/convention-sweep.md#exceptions)): Catalogue's public feed shape until Marketing tolerates omitted nulls (C-3); jobs and the image worker without `config check` until the be sdk has a worker host (D-14); the migration stamp without `+commit` until the be sdk widens its version column (D-32); Desk's HTTPS-only dev server under Ocharo's override (D-40); pipelines pinned at `e134a47` until a `v0.16.0` tag (CI). |

## Owner review

Built in v0.1 and checked by tests or the agent; the owner confirms each in the running app.

| Item | Type | Notes |
|---|---|---|
| Workspace and build identity | verification | Sign in shows only current memberships; About shows dev with the source revision on local builds. |
| Catalogue editions and languages | verification | Saved edition gives an immutable PDF matching the editorial pages; Uzbek interface edits English content; product edit refreshes a catalogue and keeps its layout; randomize then undo restores every media choice; Arabic, Persian and Urdu show RTL text and exported glyphs; a missing or unconfirmed translation drops the product from web and its bound page from PDF. |
| Image editing | verification | Editing an image leaves the source media unchanged. |
| Named catalogues | verification | Create, rename, duplicate, activate and archive keep history; content language stays independent of the interface; web-only, PDF-only and combined catalogues open the matching designer; web and PDF pages edit independently; theme, copy, media and price flags match the released reader; confirming one product language clears only its indicator; an open review question keeps the product out of public output; 1–9 and 10+ UZS prices persist and reset price confirmation; legacy pricing stays until an explicit edit; a deactivated page leaves preparation and output; unconfirmed products drop their PDF page but web keeps confirmed siblings; publication keeps prior downloads immutable; revoked approval withholds a stale PDF; a second named catalogue in the same language stays separate. |
| Native Catalogue app | verification | Open `https://localhost:8282` without Desk; product edits keep approvals, two UZS bands, image groups and history; the final active image blocks an invalid save; Uzbek interface with an English catalogue; a named Web/PDF catalogue reopens by revision and slug; whole and per-page PDF exports match the designer; explicit publish uses immutable reviewed content; a stale preparation is rejected; blurred dialogs on desktop and full forms on small screens; an existing SQLite database upgrades with IDs and history intact. |
| Catalogue header, launcher and palette | verification | Tablet and phone navigation stays reachable beside branding and Settings; branding stays clear across Uzbek, Russian, English and light and dark; the logo menu opens a responsive grid of app names; focus enters it and Escape returns; another app opens beside unsaved edits; the public reader has no launcher; chevron and hover show the menu; product media and PDF pages stay independent of the theme. |
| Catalogue settings and reliability | verification | One Settings entry holds interface, defaults and data tools; language switches keep authored copy and unsaved defaults; appearance persists after reload; the public reader's Settings holds appearance and language only; a catalogue made in the IDE-run app reopens its designers; a rotated development certificate reaches the restarted frontend; a failed request shows one localized toast and keeps confirmed content; a pending write keeps rows and forms unchanged; a successful write refreshes its region; a failed write removes nothing; refresh shows the mounted colour mode; empty and failed reads show plain labels; table refresh keeps headers and last values; cancelling a draft discard loses no edits; designers open at editing geometry; Rider resolves the local SDK. |
| Desk draft canvas | verification | The restored canvas edits pages and grids. |
| Sourcing and Events in Desk | verification | Supplier routes, people and gallery open with a stepping lightbox; the same photo twice reports the existing item; the shawl bundle twice creates nothing and keeps Desk edits; Events lists the next deadlines with days left; a shortlisted candidate keeps its notes after re-import; the owner confirms the slices with real supplier and seeded event data; populated Events, Sourcing and View settings screens after sign-in; the material-first workbench and deadline-first agenda proposal. |
| Ocharo frame | verification | Desk, Catalogue, Studio, Marketing, Editorial and Brand frames on desktop and phone. |
| Desk on dev | verification | Deploy Desk to dev through Wheelhouse and sign in as the owner. |
| Files review | verification | Product editing layouts and light and dark readability on desktop and mobile; Desk media prepared on dev reads back from Files. |

## Catalogue integration

The reader serves only explicitly published releases; see [catalogue outputs](../architecture/catalogue-outputs.md).

| Item | Type | Notes |
|---|---|---|
| Catalogue and Desk ownership | engineering | Reconcile Catalogue and authenticated Desk product and edition ownership through an explicit migration; no cross-store writes or synchronization exist. |
| Saved products | feature | Synchronize signed-in saved products; guest saving stays in the browser. |
| Application lockup | feature | Reuse the reviewed application lockup in Studio and other suitable Ocharo apps. |
| Language typography | verification | Review additional-language typography and authored translations; the 25-language registry does not imply translated content or owner approval. |

## Desk sourcing and event integrations

Design and sources: [analysis](../research/desk/2026-10-09-sourcing-events-integrations.md).

| Item | Type | Notes |
|---|---|---|
| Material requests and offers | feature | Add material requests, supplier offers, quotation evidence and sample review before price comparison. |
| Event editions and candidates | moved | Events owns editions, observations and review candidates (`events-integration #1`, `#30`); Desk reads them through its proxy. |
| Feed or provider adapter | moved | SCFD's Afisha adapter feeds Events by pull (`events-integration #6`, `#20`); Desk adds no adapter. |
| Dev bot token | fix | Revoke and replace `@ocharo_dev_bot`'s token in BotFather, then update Wheelhouse's dev vault; it was briefly pasted into a secret note, since cleared (owner, 2026-11-02). |
| Desk MCP on Identity agent tokens | engineering | Parked: an MCP client gets its own Identity OAuth token, not Desk's browser session, and MCP stays off until then (owner, 2026-11-02). The ticket-authenticated surface left on 2026-11-05; restore its read tools, UI panel and tests from commit `ce3ad7b`. Activation on the intended server and a trusted client follow. |
| Calendar OAuth | feature | Add only after calendar selection and outbound synchronization rules are agreed. |
| SDK refresh timing in Desk | engineering | Adopt it from a released frontend SDK; the 0.0.6 package lacks the orchestration exports. |

# Events adoption

*Last updated: 2026-11-10*

Desk's Events move onto WoW2 Events, the domain repo `wow-two-sdk-beta.events`: Events owns the catalog and the
tracking, and Desk keeps its page, its organization authorization and references to Ocharo entities (E1, `ocharo-ws`
`docs/app-boundaries.md`). This is the platform lane's track of the Events integration goal; the cross-lane track is
`wow-two-ws` `system/sessions/events-integration/events-integration.md`, cited as `events-integration #N`. Sources: the
Events spec (`wow-two-ws` `ideas/events-service-spec.md` § Migration from `events.showcase_events`), the 2026-11-05
review (`ideas/events-infrastructure-next.md` § Desk migration), `api-communication.md`, Identity's
`product/features/service-tokens.md` at `v0.5.21`, and [files-adoption.md](files-adoption.md) as the pattern.

Code pins `ocharo-platform` `b2cb4e3`. Row counts come from read-only queries on 2026-11-10.

## Status

- ✅ Snapshot slice verified 2026-11-10 and its deltas sent to the orchestrator (`events-integration #34`).
- 🔄 M1 picks written 2026-11-10 (#1–#11). Events' and SCFD's names arrive from their lanes through the orchestrator;
  the shapes below take them unchanged.
- 🔄 M2 started 2026-11-10 ([v0.3](../planning/version-track/v0.3/v0.3.md)): the `export events` verb is built,
  ahead of Events' published schema, so its mapping (`EventExportService`) may still rename fields; on local Desk it
  wrote 68 of 68 rows with 68 unique ids, every stage, priority, note and provenance equal to the stored row, 18
  estimated dates kept at month precision as estimated, 5 month dates announced, 10 unknown with no dates.
- ⬜ Registrations, proxy, screens, import and freeze wait for Events' and SCFD's names and `IEventsClient` (#12).

## Data

| Store | Rows | Organizations | Notes |
|---|---|---|---|
| dev `ocharo_dev` `events.showcase_events` | 0 | 0 | migration `008-events` applied; dev Desk has no operator yet |
| local Desk `brand_workspace` | 68 | 1 (`Ocharo`) | 3 bundle imports: 41 on 2026-10-08, 7 on 10-13, 20 on 10-30 |

Local field use, all 68 rows:

| Field | Use |
|---|---|
| `import_key` | 68, every row came from a bundle |
| `revision` | 1 everywhere, `updated_at` = `created_at`: nothing was edited in Desk |
| `status` | 64 `candidate`, 1 `applying`, 3 `skipped`, all as the bundles set them |
| `priority` | 6 `high`, 39 `normal`, 23 `low` |
| `date_precision` | 35 `day` (one on a 1st), 5 `month` and 18 `estimated` (all on a 1st), 10 `unknown` |
| `title` | Latin script, mostly English names; none Cyrillic; not Uzbek |
| `notes` · `participation` | 68 · 68 |
| `organizer` · `country` · `city` · `venue` | 49 · 66 · 64 · 36 |
| `ends_on` · `application_deadline` · `cost` | 38 · 9 · 25 |
| `website` · `source_url` | 46 · 51 |

The private source bundles (mft `research/assets/och-events/desk-import/<date>/`) are not on this machine; local Desk is
the only copy found.

## Picks

### #1 Import source (`events-integration #26`, accepted as `#44`)

- The producer exports local Desk's 68 rows; Events on dev imports them under dev's `Ocharo` organization.
- Dev's empty table needs no export; its writer is frozen like any other (#7).
- Reason: local Desk holds the only rows, and dev's organization id differs from the local one, so the import names its
  target organization and each record keeps its source organization in provenance.

### #2 Registrations (`events-integration #21`, `#22`)

Ocharo's Identity registry stays in `engineering/deployment/deploy.yml`, as for Files (`files-integration #32`). Indexes
are fixed, since the seeder deletes a client that leaves the settings. `<…>` marks a name the owning lane supplies.

| Key | Value | Why |
|---|---|---|
| `Resources:3:Name` | `events` | Events' audience in Ocharo's Identity |
| `Resources:3:Uri` | owner key, `url`, required | Wheelhouse types `{Events URL}/` per environment |
| `Resources:3:Scopes:N` | `events.<…>` | from the events lane |
| `Resources:3:PermissionNamespace` | `events` | an exchanged token carries only `events:*` permissions |
| `Resources:3:SignalUrl` | owner key, `url`, required | exchanged tokens carry `sid` and `org_id`, so Events hears both signals |
| `Resources:4:Name` | `harvest` | SCFD's audience, as FSS kept `files` (scfd lane, 2026-11-10) |
| `Resources:4:Uri` | owner key, `url`, required | Wheelhouse types SCFD's API site URL with its slash; on dev, product `harvestadapters`' API site (`events-integration #45`) |
| `Resources:4:Scopes:N` | the 11 `harvest.{noun}-{verb}` scopes | renamed under SCFD's resource (`events-integration #16`), such as `harvest.events-read`, `harvest.tenants-manage` |
| `Clients:8` | `events`, `client_credentials`, `Resources: [harvest]`, `harvest.events-read` only | Events pulls from SCFD as itself |
| `Clients:9` | SCFD's operator (id from the scfd lane), `client_credentials`, `Resources: [harvest]`, all 11 scopes | SCFD's operator provisions tenants, sources and subscriptions |
| `Clients:0:ExchangeResources:1` | `events` | Desk reaches Events for a person only |
| `Clients:0:Scopes:N` | Desk's `events.*` scopes | the read and tracking scopes the events lane names |

- SCFD's resource gets no `PermissionNamespace` and no `SignalUrl`: only service tokens reach it, and no signal revokes one.
- SCFD maps each client in `Harvest:ServiceAccess:ClientGrantsJson` (`{clientId: {isOperator, tenantId?, scopes}}`),
  which Wheelhouse types on the SCFD target; this repo holds only the Identity side.
- Clients 8 and 9 take typed secrets (`secret: true, required: true`, no `generate`), like client 7: Events and SCFD
  deploy as their own products, and `from: { repository, key }` links only inside one product.
- If the review console signs in through Ocharo's Identity (`events-integration #30`), client 8 adds
  `authorization_code`, its redirect URIs and a `SignalUrl`; a client registers whole, so that lands with the console.
- If Events stores images in FSS in P1 (#9), client 8 adds `Resources: files` and `files.write`.

### #3 Desk → Events for a person (`events-integration #23`)

- Desk's membership filter (`[OrganizationAccess]`) runs first; a non-member gets 404 before any token is asked.
- Desk exchanges the session's token for an `events` token (`BffSubjectTokenSource` with be sdk 10.0.109-beta's
  `OAuth2TokenExchangeHandler`, the way `AddDeskFiles` reaches Files).
- The exchanged `org_id` is the session's active organization; a route naming another one answers 404 through
  Identity.Client's route-organization requirement, before any exchange.
- Events checks a scope plus an `events:*` permission and the route's organization against `org_id` (events lane).

### #4 Roles (`events-integration #23`)

| Role | `events:*` |
|---|---|
| owner, admin | every permission the events lane names |
| editor | read and track |
| viewer | read |
| billing | none |

- Reason: Desk's rule today is reads by membership, writes by an editor or owner; Files gave viewers read the same way.

### #5 Keys Wheelhouse types before the push (`events-integration` cross-lane table)

- `Identity:Server:Resources:3:Uri`, `Identity:Server:Resources:3:SignalUrl`
- `Identity:Server:Resources:4:Uri`
- `Identity:Server:Clients:8:ClientSecret`, `Identity:Server:Clients:9:ClientSecret`, each also typed into the Events
  or SCFD target that uses it
- A required untyped key refuses every Identity deploy, so the `deploy.yml` change pushes only after ev-wheelhouse
  confirms these on dev.
- Desk's `Events:BaseUrl` links `from: Identity:Server:Resources:3:Uri`, so nobody types it.

### #6 Producer (`events-integration #26`)

- A Desk verb, `export events --organization <id> --out <bundle.json>`, beside `import events`; it reads, never writes.
- It emits `wow2.events-import.v1` as the events lane defines it, with `dryRun` left to the import.
- Each record carries `sourceId` (Desk's event id) and `key`, so Events' old → new id map is stable across reruns.
- Provenance: source `ocharo-desk`, source organization id, `created_by`, `created_at`, `revision`, `source_url`.
- Tracking keeps `status` as the stage, `priority` and `notes`; `participation` and `cost` go where the spec puts them.
- Date precision: `day` → `day`; `month` → `month` on the 1st; `estimated` → `month` with certainty `estimated`, never
  confirmed; `unknown` → no dates.
- Title language: `und` on every title, since Desk never stored one; nothing becomes `uz` by default.
- The full Desk row rides along as a `legacy` object, `scope` included, so reconciliation compares field by field.
- Reconciliation passes when the import's counts equal the export's (68), every `sourceId` maps once, and stage,
  priority, notes, provenance and precision read back equal.

### #7 Cutover (`events-integration #27`)

- `Events:BaseUrl` set: Desk's `api/organizations/{organizationId}/events` reads proxy onto `IEventsClient`.
- Tracking writes go through one Desk route onto Events' tracking; the browser still calls only Desk's routes.
- The old writes (`POST`, `PUT /{eventId}`, `DELETE /{eventId}`) answer 410 with code `EventsMoved`: catalog facts now
  change through Events' review.
- `import events` refuses while `Events:BaseUrl` is set; `events.showcase_events` stays, untouched, as the rollback copy.
- Rollback until reconciliation passes: unset `Events:BaseUrl`, and Desk reads and writes its own table again.
- After reconciliation, a later migration drops the table (its `Rollback.sql` already refuses while rows exist).

### #8 Screens (`events-integration #27`)

- The Vue screens under `apps/web/src/presentation/events` keep their behaviour and read the Events contract shape from
  Desk's proxy; `integration/events/EventsApi.ts` and `domain/events/Events.ts` change, the views stay.
- The fe package replaces them only once it matches the month feed, deadlines, detail and tracking form.

### #9 Event images (`events-integration #31`), platform input

- P2 from Desk's side: `showcase_events` has no image field, so the import moves no media.
- Desk's media sits in FSS tenant `ocharo` through resource 2 `files`; Events' catalog images belong to the catalog,
  never an organization (spec § Media), so Ocharo's tenant is the wrong owner.
- If Events needs images in P1, client 8 can reach `files` on Ocharo's tenant as a stopgap (#2); the events lane decides.

### #10 Calendar export

- Desk's `.ics` export builds UIDs from the event id (`EventCalendar.ts:85`, `:99`); after the move the ids change, so
  a re-exported file adds entries beside an old import. Accepted: the export is a one-off download, not a feed.

### #11 Service token handlers (`events-integration #24`), input

- be sdk 10.0.109-beta ships `OAuth2ClientCredentialsHandler` and `OAuth2TokenExchangeHandler` with
  `IOAuth2SubjectTokenSource`; Identity.Client 10.0.30-beta ships `BffSubjectTokenSource`. No Identity change found.

### #12 Build (M2)

1. Registrations in `deploy.yml`, pushed after #5's keys are typed.
2. `export events` verb and a dry run against local Desk; the counts go to the orchestrator.
3. The proxy, the tracking route, the 410s and the frozen verb, on the published `IEventsClient`.
4. The screens on the Events contract.
5. The import into Events on dev, then reconciliation; the rollback copy stays until it passes.

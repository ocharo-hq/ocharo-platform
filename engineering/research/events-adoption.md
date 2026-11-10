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
- ✅ M1 closed 2026-11-10 for every lane; names from the events and scfd lanes are in #2, the import schema in #6.
- 🔄 M2 started 2026-11-10 ([v0.3](../planning/version-track/v0.3/v0.3.md)):
  - ✅ `export events` writes Events' fixed `wow2.events-import.v1`; local Desk gives 68 of 68 rows (#6).
  - ✅ Registrations pushed (`34f08dc`, `v0.3.1`); `ocharo-dev`'s Identity redeployed healthy with them (#2, #5).
  - ✅ Client 10 `events-console` pushed once ev-wheelhouse typed its three URIs on `ocharo-dev`.
  - ✅ Desk's Events-backed repository built on Events `10.0.0-beta` (`WoW2.Sdk.Beta.Be.Events.Client`), be sdk
    `10.0.111-beta`: reads, proposals, tracking and the 410 freeze once `Events:BaseUrl` is set
    (#7, #8); 16 unit and 2 route tests on an in-memory Events.
  - ✅ Imported on dev 2026-11-10 by ev-wheelhouse through Events' operator verb: dry run 68 created, import 68
    created, rerun 68 unchanged. Reconciled read-only against `events_dev`: 68 of 68 rows equal to the bundle field by
    field (key, title, kind, organizer, place, dates with precision and certainty, deadlines, website, source URL,
    stage, priority, notes, participation, cost), `events_import_map` mapping each `legacyId` once.
  - ✅ Desk `v0.3.5` on dev (09:08Z), read back in the owner's own session: the Events page lists the 68 with their
    stages, precision, priority and derived scope as stored; the browser calls only Desk's origin; a fact edit and a
    delete answer 410 with the event unchanged; another organization's events path answers 404.
  - ✅ Old store retired 2026-11-10 (v0.3 Iteration 5): Desk reads only WoW2 Events, on Events Client `10.0.4-beta`,
    whose own Identity wiring replaced Desk's hand-set token exchange; no rollback to the old table remains.
  - ✅ Done-when (b), 2026-11-10: Events `v0.1.6` published the held Afisha record by automatic review
    (`classifier:filter:ocharo-fashion-v1`), and Desk's dev Events page lists it in the owner's session beside the 68
    (69 in all), "Показ мод The Fashion Show Line 2026", 21 Nov 2026, untracked so `candidate` at revision 1; the
    browser called only Desk's origin.
  - ⬜ Afisha mapping gaps for the scfd lane (`events-integration #18`): the record carries no place, so Desk derives
    `international` and shows no city, and its kind maps to `other`.

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
- Reason: local Desk holds the only rows. Local and dev share Ocharo's organization id
  (`b8c672a6-c2e4-4516-82ad-5b178e71ace1`, read 2026-11-10 from `identity.identity_organizations` on `ocharo_dev`), and
  the import's organization comes from the caller's token; each row keeps it in `legacy` as evidence.

### #2 Registrations (`events-integration #21`, `#22`)

Ocharo's Identity registry stays in `engineering/deployment/deploy.yml`, as for Files (`files-integration #32`). Indexes
are fixed, since the seeder deletes a client that leaves the settings. Names from the events and scfd lanes
(`events-integration #16`, `#21`, `#22`, `#30`), written into `deploy.yml` on 2026-11-10.

| Key | Value | Why |
|---|---|---|
| `Resources:3:Name` | `events` | Events' audience in Ocharo's Identity |
| `Resources:3:Uri` | owner key, `url`, required | Wheelhouse types `https://events.{domain}/` per environment |
| `Resources:3:Scopes:N` | `events.read`, `events.track`, `events.review`, `events.import`, `events.admin` | the events lane |
| `Resources:3:PermissionNamespace` | `events` | an exchanged token carries only `events:*` permissions |
| `Resources:3:SignalUrl` | owner key, `url`, required | `{Uri}signals/identity`: exchanged tokens carry `sid` and `org_id`, so Events hears both signals |
| `Resources:4:Name` | `harvest` | SCFD's audience, as FSS kept `files` (scfd lane, 2026-11-10) |
| `Resources:4:Uri` | owner key, `url`, required | Wheelhouse types SCFD's API site URL with its slash; on dev, product `harvestadapters`' API site (`events-integration #45`) |
| `Resources:4:Scopes:N` | the 11 `harvest.{noun}-{verb}` scopes | renamed under SCFD's resource (`events-integration #16`), such as `harvest.events-read`, `harvest.tenants-manage` |
| `Clients:8` | `events`, `client_credentials`, `Resources: [harvest]`, `harvest.events-read` only | Events pulls from SCFD as itself |
| `Clients:9` | `harvest-operator`, `client_credentials`, `Resources: [harvest]`, all 11 scopes | SCFD's operator provisions tenants, sources and subscriptions |
| `Clients:10` | `events-console`, code flow plus `token_exchange` to `events` with all 5 `events.*` scopes, namespace `events`, typed redirect, sign-out and signal URIs | the review console's BFF sign-in (`events-integration #30`); a reviewer needs `events:review` in the catalog's owner organization; pushed in a follow-up once its URIs were typed, so no Identity deploy waited on the console |
| `Clients:0:ExchangeResources:1` | `events` | Desk reaches Events for a person only |
| `Clients:0:Scopes:2–4` | `events.read`, `events.track`, `events.import` | Desk reads, tracks and imports for a person |

- SCFD's resource gets no `PermissionNamespace` and no `SignalUrl`: only service tokens reach it, and no signal revokes one.
  Identity `v0.5.20` (Ocharo's image) keeps a resource's `SignalUrl` optional: `OidcResourceOptions.SignalUrl` is
  nullable ("null hears none") and the options validator checks only the URLs named (`events-integration #49`).
- SCFD maps each client in `Harvest:ServiceAccess:ClientGrantsJson` (`{clientId: {isOperator, tenantId?, scopes}}`),
  which Wheelhouse types on the SCFD target; this repo holds only the Identity side.
- Clients 8–10 generate their secrets on `ocharo-dev`; Wheelhouse's owner link carries each to the Events or SCFD
  target, since `from: { repository, key }` links only inside one product (ev-wheelhouse, P1).
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
| owner, admin | `events:read`, `events:track`, `events:review`, `events:admin` |
| editor | `events:read`, `events:track` |
| viewer | `events:read` |
| billing | none |

- Reason: Desk's rule today is reads by membership, writes by an editor or owner; Files gave viewers read the same way.

### #5 Keys Wheelhouse types before the push (`events-integration` cross-lane table)

- `Identity:Server:Resources:3:Uri`, `Identity:Server:Resources:3:SignalUrl`
- `Identity:Server:Resources:4:Uri`
- `Identity:Server:Clients:10:RedirectUris:0`, `…:PostSignOutRedirectUris:0`, `…:SignalUrl`: the review console's URIs
- No secret: clients 8–10 generate theirs, and the owner link carries them to the Events and SCFD targets
- A required untyped key refuses every Identity deploy, so the `deploy.yml` change pushes only after ev-wheelhouse
  confirms these on dev.
- Pushed 2026-11-10 without client 10 once ev-wheelhouse typed keys 1–3 (resources 3 and 4); client 10 followed
  the same day, once its three URIs were typed: `{Uri}api/identity/callback`, `{Uri}`, `{Uri}api/identity/signal`.
- Desk's `Events:BaseUrl` links `from: Identity:Server:Resources:3:Uri`, so nobody types it.

### #6 Producer (`events-integration #26`)

- A Desk verb, `export events --organization <id> --out <bundle.json>`, beside `import events`; it reads, never writes.
- It emits Events' `wow2.events-import.v1` (Events repo `engineering/architecture/contract.md` § Imports, fixed
  2026-11-10): `{ format, source: "ocharo-desk", rows }`; the importing caller's token names the organization.
- Each row's `legacyId` is Desk's event id, so Events' old → new id map is stable across reruns.
- `facts`: `key`, `title { und }`, `kind` (Desk's `format`), `organizer { name }`, `place { venue, city, country }`,
  `starts`, `ends`, `deadlines [{ kind: application, at }]`, `website`; `scope` is dropped as derived.
- Dates: `day` → `yyyy-MM-dd` at `day`; `month` → `yyyy-MM` at `month`, never a first-of-month day; `estimated` →
  `yyyy-MM` at `month`, certainty `estimated`, never announced or confirmed; `unknown` → `{ precision: unknown }`, no value.
- Title language: `und` on every title, since Desk never stored one; nothing becomes `uz` by default.
- `sourceUrl`, `recordedAtUtc` (Desk's `created_at`), and `legacy`: every column of the row, organization and
  `created_by` included, which Events stores as an `import` observation.
- `tracking { stage, priority, notes, fields: { participation, cost } }`; Desk's 8 stages go into Ocharo's workflow
  first (`PUT api/events/v1/workflow`, `attended` won, `rejected` lost).
- On local Desk, 2026-11-10: 68 rows, 68 unique `legacyId`s, 35 day and 23 month values in shape, 18 estimated, 10
  unknown with no value, no end before its start, tracking and `recordedAtUtc` equal to the stored row.
- Reconciliation passes when Events' `EventImportReport` maps all 68 `legacyId`s once and stage, priority, notes,
  fields and precision read back equal.

### #7 Cutover (`events-integration #27`)

- `Events:BaseUrl` set: Desk's `api/organizations/{organizationId}/events` reads proxy onto `IEventsClient`.
- Create maps onto Events' member proposal (`POST …/catalogs/{c}/candidates`, `events.track` + `events:track`): the
  event is a candidate Ocharo tracks at once, published only when a reviewer accepts it.
- Tracking writes go through one Desk route onto Events' `PUT …/tracking/{eventId}`; the browser still calls only Desk's
  routes.
- The old fact writes (`PUT /{eventId}`, `DELETE /{eventId}`) answer 410 with code `EventsMoved`: catalog facts change
  through Events' review, and untracking uses the tracking route.
- The import runs in Events' review console (`/imports`), revised 2026-11-10 by the events lane: the developer signs in
  as Ocharo's owner (`events:admin`; Ocharo's only dev member, also the instance admin), dry-runs, then imports;
  catalog `ocharo-fashion` and Ocharo's workflow come from Events host verbs. Desk's exchange grant stays
  `events.read`, `events.track`, `events.import`.
- Retired 2026-11-10 once reconciliation passed (v0.3 Iteration 5, the developer: "yes drop completely"): `import
  events`, `export events`, the table repository and `IEventRecordsReader` are gone, migration `014-events-retire`
  drops `events.showcase_events`, and `Events:BaseUrl` is required in a deployed run. There is no rollback to Desk's
  own table; the export bundle stays outside Git, and Events' `events_import_map` keeps the 68 old → new ids.

### #8 Adapter and screens (`events-integration #27`), revised 2026-11-10

- Desk keeps its `IEventRepository` port; with `Events:BaseUrl` set, an Events-backed implementation replaces
  `EventRepository`, so `EventsController`, its handlers and the Vue screens stay unchanged in P1.
- Reads: Events' tracking list of catalog `ocharo-fashion` (`trackedOnly=false`), each `TrackedEventModel` mapped to a
  `ShowcaseEvent`: facts from `event`, stage, priority, notes and `fields` from `tracking`, its revision (0 untracked).
- An accepted event Ocharo never tracked shows as `candidate`, `normal`, so done-when (b) needs no tracking first.
- Dates map back: `day` → the day; `month` → the 1st, `month`, or `estimated` when its certainty is estimated;
  `unknown` → no dates; the title prefers `und`, then `en`, `uz`, `ru`.
- Summary: Events' tracking summary; create: a member proposal plus its tracking; update: tracking when only stage,
  priority, notes, participation or cost changed, else 410 `EventsMoved`; delete: 410 `EventsMoved` (#7).
- Built on the published Events Client (`AddEventsClient()` with the be sdk's `AddOAuth2TokenExchange` and
  `BffSubjectTokenSource`); nothing is written against unpublished wire types.
- The fe package replaces the screens only once it matches the month feed, deadlines, detail and tracking form.
- Desk's revision is the tracking revision plus one: an untracked event reads as 1, since Desk's screens require a
  positive revision, and a write at 1 creates the tracking.
- Fact edits and deletes answer 410 `Gone`; a stale revision 409, as before; bundle imports (`import events`, keyed
  creates) answer 410.
- Desk's filters, order and paging run in memory over the whole catalog, the rules of its own SQL; Events' query
  takes them over with the fe package.
- While Events serves Desk, Desk validates organizer and city at Events' 200 characters, not its table's 160 and 120:
  Desk's form sends an event's facts back with every tracking save, so the narrower limits refused stage saves on
  events Events accepted (cross-lane review, 2026-11-10).

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

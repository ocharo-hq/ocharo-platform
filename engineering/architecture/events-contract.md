# Events contract — showcase events feed

*Last updated: 2026-11-10*

## Increment

Desk Events, slice 1: one feed of every event where Ocharo could show its products — local
(Uzbekistan), regional (Central Asia and neighbours) and international — with dates, deadlines,
how to take part, what it costs, and where Ocharo stands with each one. Event facts belong to Desk,
as the brand identity and events proposal (mft `ventures/ocharo/platform/och-platform-brand-identity-and-events.md`)
assigns them; Marketing later attaches campaign work to an event reference. Desk never applies,
registers or pays on Ocharo's behalf.

Placement: a Desk section (`/organizations/{organizationId}/events`), built like
[Sourcing](sourcing-contract.md). Every rule of the [preparation contract](preparation-contract.md)
applies: membership filter, organization-scoped queries, composite keys, 404 for foreign rows,
`X-CSRF-TOKEN` on unsafe requests, `{ data }` envelopes on the SDK JSON (null members left out; the types below show them as `null`, which the
frontend decoders restore) and SDK problem details, whose `code` names the error kind.

## Record

| Field | Rule |
|---|---|
| `id` | `(organization_id, id)` |
| `key` | optional import slug `^[a-z0-9][a-z0-9-]{0,63}$`, unique per organization; null for events added in Desk |
| `title` | 1–200 |
| `organizer` | ≤ 160 or null |
| `scope` | `local` · `regional` · `international` — relative to Ocharo in Uzbekistan |
| `format` | `runway` · `trade-fair` · `market` · `exhibition` · `competition` · `festival` · `conference` · `other` |
| `country` | ISO 3166-1 alpha-2 upper case, or null |
| `city`, `venue` | ≤ 120 / ≤ 200, or null |
| `startsOn`, `endsOn` | ISO dates or null; `endsOn ≥ startsOn` |
| `datePrecision` | `day` · `month` (only the month is announced) · `estimated` (inferred from earlier editions) · `unknown` (dates null). For `month`/`estimated`, `startsOn` is the 1st of that month |
| `applicationDeadline` | ISO date or null |
| `participation` | how to take part, ≤ 2000 ("application package to the organiser", "stand booking") |
| `cost` | as stated, ≤ 500, or null — unknown cost stays null, never `0` |
| `website`, `sourceUrl` | http(s) ≤ 2048, or null — official page, and where the event was found |
| `status` | `candidate` (found, not reviewed) · `shortlisted` · `applying` · `applied` · `confirmed` · `attended` · `skipped` · `rejected` |
| `priority` | `high` · `normal` · `low` |
| `notes` | ≤ 8000 — fit, what to show, evidence; never private prices or personal assessments |
| `revision`, `createdAt`, `updatedAt` | optimistic concurrency as in Sourcing |

Validation: `startsOn`/`endsOn`/`applicationDeadline` between 2000-01-01 and 2100-12-31;
`datePrecision = unknown` requires null dates; `day` requires `startsOn`. Blank optional strings → null.

## HTTP surface

Base: `/api/organizations/{organizationId}/events`. Reads need membership; writes need `CanEdit`.

| Method | Path | Body / query | Returns `data` |
|---|---|---|---|
| GET | `` | `query?`, `scope?`, `format?`, `status?`, `from?`, `to?` (ISO dates, filter by the event's last day ≥ from — `endsOn`, else the month's last day for `month`/`estimated`, else `startsOn` — and `startsOn` ≤ to), `includeUndated` (default true), `limit` (≤ 500, default 200), `offset` | `ShowcaseEvent[]` ordered by `startsOn` (nulls last), then title |
| GET | `/summary` | `today?` (ISO date, defaults to server UTC date) | `{ byStatus: Record<status, number>, upcomingDeadlines: ShowcaseEvent[] }` — up to 5 with `applicationDeadline ≥ today`, status not `skipped`/`rejected`, soonest first |
| POST | `` | all mutable fields; `status` defaults to `shortlisted`, `priority` to `normal`, `datePrecision` to `day` when `startsOn` is set else `unknown` | `ShowcaseEvent` |
| GET | `/{eventId}` | — | `ShowcaseEvent` |
| PUT | `/{eventId}` | `{ expectedRevision, …all mutable fields }` | `ShowcaseEvent` |
| DELETE | `/{eventId}` | `expectedRevision` query | `{ id }` |

`query` matches title, organizer, city, venue, country and key (case-insensitive, `%`/`_` escaped).

```ts
type EventScope = 'local' | 'regional' | 'international';
type EventFormat = 'runway' | 'trade-fair' | 'market' | 'exhibition' | 'competition' | 'festival' | 'conference' | 'other';
type EventStatus = 'candidate' | 'shortlisted' | 'applying' | 'applied' | 'confirmed' | 'attended' | 'skipped' | 'rejected';
type DatePrecision = 'day' | 'month' | 'estimated' | 'unknown';
type EventPriority = 'high' | 'normal' | 'low';

interface ShowcaseEvent {
  id: string; key: string | null; title: string; organizer: string | null;
  scope: EventScope; format: EventFormat; country: string | null; city: string | null; venue: string | null;
  startsOn: string | null; endsOn: string | null; datePrecision: DatePrecision; applicationDeadline: string | null;
  participation: string; cost: string | null; website: string | null; sourceUrl: string | null;
  status: EventStatus; priority: EventPriority; notes: string;
  revision: number; createdAt: string; updatedAt: string;
}
```

## Feed import

New events enter in bulk through an explicit bundle — the same path for the first seed and for any
later discovery scan:

```bash
dotnet run --project BrandWorkspace.Api --no-build --no-launch-profile -- \
  import events --bundle <bundle.json> --organization <organization-id> --actor <identity-user-id|email> [--dry-run]
```

Format `ocharo.events-import.v1`: `{ format, source, events: [ { key, …record fields except id/revision/timestamps } ] }`.
Every event needs a `key`. The command validates the whole bundle before writing, prints a JSON
summary `{ dryRun, source, created, existing, failures[] }`, and never overwrites an existing key,
so statuses and notes edited in Desk survive every re-import. Imported events default to `candidate`.
`--actor` names a person Desk has seen sign in through Ocharo's Identity: their Identity user id (`sub`),
or the email their sign-in carried when one person holds it. Ocharo people sign in by phone and carry no email, so
the id is what works for them. Identity keeps every role, and a verb runs without a sign-in, so the person imports as
an editor of an organization Desk knows; an unknown person or organization refuses.

## Move to WoW2 Events

Events move onto WoW2 Events, which owns the catalog and tracking; Desk keeps this page and proxies it
([events-adoption.md](../research/events-adoption.md), v0.3). The move starts from an export:

```bash
dotnet run --project BrandWorkspace.Api --no-build --no-launch-profile -- \
  export events --organization <organization-id> --out <bundle.json>
```

It writes Events' `wow2.events-import.v1` (events-adoption.md #6): one row per event keyed by its Desk id
(`legacyId`), its facts with `und` titles, `yyyy-MM` months and `estimated` kept, Ocharo's tracking with participation
and cost as fields, and the whole stored row as `legacy`. It reads Desk, writes only the new file, refuses an existing
one, and prints the counts by stage and precision that reconciliation compares. Bundles stay out of Git, like the
import's.

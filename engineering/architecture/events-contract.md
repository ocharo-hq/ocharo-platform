# Events contract — the Events page on WoW2 Events

*Last updated: 2026-11-20*

## Increment

One feed of every event where Ocharo could show its products — national (Uzbekistan), Central Asia and
international — with dates, deadlines, how to take part, what it costs, and where Ocharo stands with
each one. WoW2 Events (`wow-two-sdk-beta.events`) owns the catalog `ocharo-fashion`, the organization's tracking and its
workflow; the Ocharo Events app is their Ocharo face ([events-app.md](../research/events-app.md)). It never applies,
registers or pays on Ocharo's behalf; Marketing later attaches campaign work to an event reference.

## Wire

Since OE07 (2026-11-19) the page reads WoW2 Events' own contract, unchanged: `EventModel`, `TrackedEventModel`,
`TrackingModel`, `TrackingSummaryModel` and `WorkflowModel`, rendered by `@wow-two-beta/fe-events`' kit. The app keeps
no record, mapper or decoder of its own, so a new fact Events carries reaches the page with a package re-pin, not an
edit here. The Desk-era record, its mapper, the full `PUT`, the `DELETE` and the 200-row in-memory list left with it.

## HTTP surface

Base: `/api/organizations/{organizationId}/events-api/api/events/v1` — Events' routes, mirrored under the organization
as the digest routes are under `…/delivery`. The organization must be the session's active one, with `events:read`;
anything else answers 404. Unsafe requests carry `X-Csrf-Token`. Answers are Events' `{ data }` envelope or a problem.

| Method | Path | Query / body | Needs |
|---|---|---|---|
| GET | `catalogs/{catalog}/tracking` | `stage`, repeated `stages` / `excludeStages`, repeated `countries` / `excludeCountries`, `includeUnknownCountry`, `kind`, repeated `kinds`, `city`, repeated `cities`, repeated `sources` (a source name or `curated`), `status`, `text`, `from`, `to`, `publishedSince`, `priority`, `trackedOnly`, `deadlineBefore`, `includeUndated`, `sort` (`starts`, `added`, `deadline`), `cursor`, `limit` | read |
| GET | `catalogs/{catalog}/tracking/facets` | the tracking list's filters; answers `total` and counts per country, city, kind, stage, priority, source and open deadline, each counted without its own selection | read |
| GET | `catalogs/{catalog}/tracking/summary` | `today`, repeated `deadlineKinds` | read |
| GET | `catalogs/{catalog}/tracking/{eventId}` | — | read |
| PUT | `catalogs/{catalog}/tracking/{eventId}` | `TrackingSpec` (`stage`, `priority`, `notes`, `fields`, `references`, `expectedRevision`) | `events:track` |
| DELETE | `catalogs/{catalog}/tracking/{eventId}` | `expectedRevision` | `events:track` |
| GET | `catalogs/{catalog}/tracking/{eventId}/history` | — | read |
| GET | `catalogs/{catalog}/events/{id}` | — | read |
| GET | `catalogs/{catalog}/events/{id}/images/{index}` | 302 to a short-lived signed WoW2 Files URL of SCFD's stored copy of `facts.images[index]`, else to the image's own https `url`; no body | read |
| POST | `catalogs/{catalog}/candidates` | `EventSpec` (`facts`) | `events:track` |
| GET | `workflow` | — | read |
| PUT | `workflow` | `WorkflowSpec` | `events:admin` |
| GET | `catalogs/{catalog}/events.ics` | event filters (`from`, `kind`, `text`, `country`, `review`) | read |

- Every list filter, the cursor and the page size cross to Events as sent: one list request is one Events call, filtered
  and paged in Events' database.
- A catalog other than `Events:Catalog` (`ocharo-fashion`) answers 404, an unconfigured Events 503, before any call;
  Events checks the exchanged token's scope and permission again.
- The image route reads the event through Events, takes the file id from `facts.images[index].fileRef` (never one the
  browser sends) and asks WoW2 Files, by a read-only key of SCFD's tenant (`Files:BaseUrl`, `Files:ApiKey`), for a
  5-minute signed link, answered as 302 with `Cache-Control: private, max-age=60`. When Files is unconfigured, fails or
  answers late (5 s), the route answers 302 to the image's own `url` instead, only if that is an absolute https link without
  credentials (else 404), so a card keeps its source photo until the copy is served. 404 for an index out of range and an
  image with no `fileRef`. An image's `url` stays the source link that credits it; the page's client shows a copy by giving
  the kit this route as the image's address, and an image without a copy keeps the kit's own rendering (the source image,
  then the date tile).
- A member's proposal publishes at once: Ocharo's catalog is private to the organization (Events OE03), so the feed
  lists it with no review step. An event's facts change only through Events' review.

## Ocharo's configuration of the kit

| Item | Value |
|---|---|
| Stages | `candidate` (open, the default: an untracked event reads as it) · `shortlisted` (open) · `applying`, `applied`, `confirmed` (active) · `attended` (won) · `skipped`, `rejected` (lost: out of the nearest deadlines) |
| Stage labels | en, uz and ru, from the app's interface messages |
| Custom fields | `participation` "How to take part" (≤ 2000), `cost` "Ocharo's cost" (≤ 500, unknown stays empty, never `0`), `sourceUrl` "Source link" (read-only: shown, never edited, kept on save) |
| Workflow in Events | the deploy writes it from `deployment/events/ocharo-workflow.json` with Events' `workflow put --spec` (`deployment/deployment.md` § Events workflow); the page only reads it and shows Ocharo's labels from the spec for what the stored one lacks |
| Kinds | `runway` · `trade-fair` · `market` · `exhibition` · `competition` · `festival` · `conference` · `other`, labelled in en, uz and ru |
| Scopes | derived from the country: `national` "National" (UZ) · `central-asia` "Central Asia" (KZ, KG, TJ, TM) · `international` "International" (any other country, Afghanistan, Azerbaijan and Russia included); no country reads "Scope unknown" and no chip selects it. The feed's scope chips and the calendar's scope pills share these keys and labels (`ocharoRegions()`) |
| Calendar scopes | three pills above the calendar choose scopes (several at once; none chosen shows every event, those with no country included), narrowing the loaded window's events in the browser by each event's country through the feed's rule (`regionOfCountry`); each pill counts the window's drawn events in its scope; the choice lives in the address as `?scope=national,central-asia` |
| Feed | skipped and rejected hidden until "Show Skipped, Rejected" or a stage filter asks; quick Shortlist and Skip on untracked cards; Ocharo's cost on each card |
| Deadlines | the five nearest `application` deadlines, untracked events included, cancelled and lost-stage events left out |
| Past, not closed | ended events in `shortlisted`, `applying`, `applied` or `confirmed`: one list call (`stages`, `to` = yesterday, `includeUndated=false`), kept when their last day has passed; Attended or Skipped is one tracking write at the revision read (OE12) |
| Proposal | "Add event": starts shortlisted, normal priority, with an application deadline and where it was found (kept in `sourceUrl`) |

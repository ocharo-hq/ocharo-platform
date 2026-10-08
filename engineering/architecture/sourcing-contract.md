# Sourcing contract — suppliers, contacts and shared media

*Last updated: 2026-10-08*

## Increment

Desk Sourcing, slice 1: an operator records material suppliers, the routes that reach them, the named
people behind those routes, and the photos, PDFs and videos suppliers shared. Searches, offers, currency
conversion and reference-fit grading are later slices. The app records outreach; it never sends.

Placement was decided on 2026-10-08: a Desk section (`/organizations/{organizationId}/sourcing`), not a
separate loopback app. Every rule of the [preparation contract](preparation-contract.md) applies:
membership filter, organization-scoped queries, composite foreign keys, 404 for foreign rows,
`X-CSRF-TOKEN` on unsafe requests, `{ data }` envelopes and `PreparationException` problem codes.

## Records

| Record | Identity | Mutable fields | Notes |
|---|---|---|---|
| Supplier | `(organization_id, id)`; optional `key` unique per organization | name, stage, country, city, website, notes | `stage`: `lead` · `contacted` · `replied` · `quoted` · `qualified` · `dropped`. Import never sets `qualified`; qualification stays an explicit review step |
| Person | `(organization_id, id)` under one supplier | name, role, notes | Deleting a person keeps its routes and clears their `personId` |
| Contact route | `(organization_id, id)` under one supplier | channel, destination, label, sourceUrl, verified, isPrimary, personId, notes | `channel`: `whatsapp` · `email` · `phone` · `form` · `website` · `instagram` · `facebook` · `telegram` · `marketplace` · `other`. Unique per supplier by `(channel, destination)`; one primary per supplier. The same destination may serve several suppliers (shared routes) |
| Media | `(organization_id, id)`; `sha256` unique per organization | supplierId (null = inbox), capture, caption | Bytes are immutable and content-addressed; `capture`: `original` · `screenshot` · `derived` |

Every mutable row carries `revision` (starts at 1). Updates and deletes send `expectedRevision`;
a mismatch returns 409 `conflict`. Text limits: names 1–160, role/label/city 120, destination 512,
URLs 2048, notes 8000 (supplier) / 4000 (person, route), caption 2000, source label 200.
Country is ISO 3166-1 alpha-2 upper case. Blank optional strings normalize to `null`; notes and caption to `""`.

## Media rules

| Content type | Accepted when | Limit | Derived data |
|---|---|---|---|
| `image/jpeg`, `image/png`, `image/webp` | SDK probe decodes one frame ≤ 8192 px per side, ≤ 50 MP | 20 MiB | WebP thumbnail ≤ 480 px (auto-oriented), width/height, EXIF `takenAt`, `hasLocation`, 64-bit perceptual hash |
| `application/pdf` | starts with `%PDF-` | 25 MiB | none |
| `video/mp4` | bytes 4–7 are `ftyp` | 64 MiB | none |

- SHA-256 is computed server-side over the original bytes; an upload whose hash already exists in the
  organization returns the existing item with `duplicate: true` and stores nothing.
- Near duplicates: images whose perceptual hashes differ by ≤ 6 bits are returned as `nearDuplicateIds`;
  they are warnings, never automatic merges.
- Original bytes are served unchanged (GPS included) to members only; `hasLocation` lets the UI warn
  before anything leaves Desk. Content responses send `nosniff`, `no-store` and support byte ranges.

## HTTP surface

Base: `/api/organizations/{organizationId}/sourcing`. Reads need membership; writes need `CanEdit`.

| Method | Path | Body / query | Returns `data` |
|---|---|---|---|
| GET | `/suppliers` | `query?`, `stage?`, `limit` (≤ 200, default 50), `offset` | `SupplierSummary[]` ordered by name |
| POST | `/suppliers` | `{ name, stage?, country?, city?, website?, notes? }` | `Supplier` |
| GET | `/suppliers/{supplierId}` | — | `Supplier` |
| PUT | `/suppliers/{supplierId}` | `{ expectedRevision, name, stage, country, city, website, notes }` | `Supplier` |
| POST | `/suppliers/{supplierId}/people` | `{ name, role?, notes? }` | `SupplierPerson` |
| PUT | `/people/{personId}` | `{ expectedRevision, name, role, notes }` | `SupplierPerson` |
| DELETE | `/people/{personId}` | `expectedRevision` query | `{ id }` |
| POST | `/suppliers/{supplierId}/routes` | `{ channel, destination, label?, sourceUrl?, verified, isPrimary, personId?, notes? }` | `ContactRoute` |
| PUT | `/routes/{routeId}` | `{ expectedRevision, channel, destination, label, sourceUrl, verified, isPrimary, personId, notes }` | `ContactRoute` |
| DELETE | `/routes/{routeId}` | `expectedRevision` query | `{ id }` |
| GET | `/media` | `supplierId?` or `unassigned=true`, `limit` (≤ 200, default 100), `offset` | `SourcingMedia[]`, newest `receivedAt` first, then newest upload |
| GET | `/media/summary` | — | `{ total, unassigned }` |
| POST | `/media` | multipart: one `file`; optional `supplierId`, `capture` (default `original`), `caption` | `{ media: SourcingMedia, duplicate, nearDuplicateIds }` |
| PUT | `/media/{mediaId}` | `{ expectedRevision, supplierId, capture, caption }` | `SourcingMedia` |
| POST | `/media/assign` | `{ mediaIds (1–200), supplierId }` (`null` returns them to the inbox) | `{ updated }` |
| DELETE | `/media/{mediaId}` | `expectedRevision` query | `{ id }` |
| GET | `/media/{mediaId}/content` | — | original bytes |
| GET | `/media/{mediaId}/thumbnail` | — | `image/webp`; 404 for PDFs and videos |

Setting `isPrimary` on a route clears the supplier's previous primary in the same transaction.
`personId` and `supplierId` values must belong to the route's supplier / the organization, or the
request fails with 400 `validation` (never 404, which would leak other organizations' identifiers).

### Response shapes (camelCase JSON)

```ts
type SupplierStage = 'lead' | 'contacted' | 'replied' | 'quoted' | 'qualified' | 'dropped';
type ContactChannel = 'whatsapp' | 'email' | 'phone' | 'form' | 'website' | 'instagram' | 'facebook' | 'telegram' | 'marketplace' | 'other';
type MediaCapture = 'original' | 'screenshot' | 'derived';

interface SupplierSummary {
  id: string; key: string | null; name: string; stage: SupplierStage;
  country: string | null; city: string | null;
  primaryRoute: { channel: ContactChannel; destination: string } | null;
  routeCount: number; personCount: number; mediaCount: number;
  coverMediaId: string | null;          // newest image with a thumbnail
  revision: number; updatedAt: string;
}
interface Supplier {
  id: string; key: string | null; name: string; stage: SupplierStage;
  country: string | null; city: string | null; website: string | null; notes: string;
  people: SupplierPerson[]; routes: ContactRoute[];   // people by name; routes primary first, then channel, destination
  revision: number; createdAt: string; updatedAt: string;
}
interface SupplierPerson {
  id: string; supplierId: string; name: string; role: string | null; notes: string;
  revision: number; createdAt: string; updatedAt: string;
}
interface ContactRoute {
  id: string; supplierId: string; personId: string | null; channel: ContactChannel; destination: string;
  label: string | null; sourceUrl: string | null; verified: boolean; isPrimary: boolean; notes: string;
  revision: number; createdAt: string; updatedAt: string;
}
interface SourcingMedia {
  id: string; supplierId: string | null; sha256: string; contentType: string; byteSize: number;
  width: number | null; height: number | null; hasThumbnail: boolean;
  capture: MediaCapture; originalName: string; caption: string;
  sourceLabel: string | null; sourceMessageId: string | null;
  receivedAt: string | null; takenAt: string | null; hasLocation: boolean;
  revision: number; createdAt: string; updatedAt: string;
}
```

## Import bundle

Private supplier records enter Desk only through an explicit bundle file and the CLI:

```bash
dotnet run --project BrandWorkspace.Api --no-build --no-launch-profile -- \
  --import-sourcing --bundle <bundle.json> --organization <organization-id> --actor <operator-email> [--dry-run]
```

The command validates the whole bundle before writing, prints a JSON summary, and is idempotent:
existing suppliers (by `key`), people (by supplier + case-insensitive name), routes (by supplier +
channel + destination) and media (by SHA-256) are counted as `existing` and never overwritten, so edits
made in Desk survive a re-import. A declared `sha256` that differs from the file's bytes fails that item.
`--dry-run` validates and reports without writing. The actor must be a member who can edit.

```jsonc
{
  "format": "ocharo.sourcing-import.v1",
  "source": "free-text provenance, e.g. ledger path + sha256 + snapshot time",
  "suppliers": [{
    "key": "swi", "name": "…", "stage": "quoted", "country": "IN", "city": null, "website": "https://…", "notes": "…",
    "people": [{ "key": "navit-sethi", "name": "Navit Sethi", "role": null, "notes": "" }],
    "routes": [{ "channel": "whatsapp", "destination": "+91…", "label": null, "sourceUrl": "https://…",
                 "verified": true, "isPrimary": true, "personKey": "navit-sethi", "notes": "" }]
  }],
  "media": [{ "supplierKey": "swi", "path": "relative/to/bundle/or/absolute.png", "sha256": "…",
              "capture": "screenshot", "originalName": "och-swi-received-01.png", "caption": "",
              "sourceLabel": "WhatsApp 2026-09-29", "sourceMessageId": null, "receivedAt": "2026-09-29T11:54:00Z" }]
}
```

Bundles contain private contact data; they stay in the private business workspace and never enter Git.
The ledger-to-bundle mapping is [engineering/scripts/sourcing](../scripts/sourcing/).

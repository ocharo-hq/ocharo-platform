# ventures.ocharo-catalogue — Architecture

*Last updated: 2026-10-19*

> Retained local-pilot architecture. Canonical authenticated authoring and named web/PDF editions
> are documented in [Catalogue authoring and outputs](../catalogue-outputs.md). The pilot remains
> preserved and offers an explicit Desk link; its local data is not a second canonical write path.
> The older runtime examples below describe that retained implementation, not the current Desk entrypoint.

```mermaid
flowchart LR
  WS[Maftuna workspace<br/>portfolio/products/*/och-*.json] -->|read-only import, SHA-256| API
  UI[Vue operator app<br/>/] --> API[OcharoCatalogue.Api]
  API --> DB[(SQLite<br/>document rows + revisions)]
  API --> Blobs[(Blob store<br/>originals + WebP previews)]
  API -->|/api/public/catalogue| MKT[Ocharo Marketing]
  API -->|/api/public/catalogue| BLOG[Ocharo Blog]
```

| Project | Responsibility |
|---|---|
| `OcharoCatalogue.Api` | Controllers, local-pilot boundary (loopback, no cross-origin writes), ProblemDetails mapping, seed-on-empty |
| `OcharoCatalogue.Application` | `ProductService` (validation, revisions, bulk status/prices), `CollectionService`, `SettingsService`, `PublicCatalogueService`, `CatalogueExportService` |
| `OcharoCatalogue.Domain` | `ProductDocument` (locales, pricing tiers, media, review issues, provenance), `CollectionDocument`, `CatalogueSettings`, records |
| `OcharoCatalogue.Infrastructure` | `MediaStore` (kit `IBlobRepository`, SkiaSharp WebP previews), `WorkspaceCatalogueImporter` |
| `OcharoCatalogue.Persistence` | EF Core SQLite; saves check the expected revision inside a transaction and write a snapshot |

## Contracts

- `GET /api/public/catalogue?locale=uz|ru|en` — `{ brand, currency, locale, generatedAt, priceNote, garmentTypes, collections[], products[] }`; products carry copy for the requested locale plus all locales, pricing tiers, and image URLs (`/api/catalogue/media/{sha}` + `/preview`).
- Media URLs are content-addressed and immutable (1-year cache).
- Editing API under `/api/catalogue/*` is loopback-only and revision-checked (`expectedRevision`, 409 on conflict).
- Stock: `GET /api/catalogue/products/stock`, `PUT /api/catalogue/products/{id}/stock` `{ expectedRevision, stock: {size: pieces} | null, madeToOrder }`; the public feed adds `availability` and `availableSizes` only.
- Copy grid: `GET /api/catalogue/products/copy`, `PUT /api/catalogue/products/{id}/copy` `{ expectedRevision, locales }` — replaces only the three-language copy.

## Brand

- Print sheets read Ocharo Brand's public kit in the browser (`VITE_OCHARO_BRAND_URL`, dev `http://localhost:8287/`): the published on-light logo file replaces the text wordmark, and brand links lead the contact lines (catalogue lines that repeat a brand handle are dropped). Brand offline → text wordmark + catalogue contact lines.

## SDK usage and extraction candidates

- Uses the published kit `WoW2.Sdk.Beta.Be 10.0.71-beta` (blob storage, mediator registration) and `@wow-two-beta/fe 0.0.23` (HTTP client, router, error boundary, icons).
- Product-local candidates for later SDK extraction: revisioned document store (document row + snapshot + expected-revision save), content-addressed media store with previews, local-pilot boundary middleware, CSV formula-safe writer, lazily resolved SQLite/blob paths.

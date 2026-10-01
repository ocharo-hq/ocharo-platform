> Adopted source reference: Ocharo Platform owns this companion. Shared runtime integration is deferred.

# Ocharo Catalogue

Maftuna's catalogue editor: the source of truth for Ocharo products, prices, images and collections. Marketing and the Blog read its public feed.

## What it does

- **Products** — garment family, variant, sizes, fabric/decoration codes, tags, three-language copy (uz/ru/en) with per-language approval, review questions, internal notes.
- **Copy grid (Matnlar)** — every product's uz/ru/en name, fabric, decoration and description in one table with per-language approval; saves only the copy, revision-checked.
- **Stock (Qoldiq)** — pieces on hand per size, or made-to-order; the feed exposes only availability (in stock / sold out / made to order) and available sizes, never counts.
- **Prices** — quantity tiers in UZS (1 piece / 10+), confirmed vs draft, one-sheet inline editing, bulk ±% with rounding.
- **Images** — content-addressed originals (SHA-256) with WebP previews; roles (fonsiz, modelda, namuna, surat, detal, mato), order, alt text.
- **History** — every save is an immutable revision; restore saves an old snapshot as the newest revision.
- **Collections** — ordered selections (seasons, lookbooks, merchant sheets), optionally published to the feed.
- **Print sheet** — brand logo and links from Ocharo Brand; lookbook / price list per language, plus a merchant **order sheet** (sizes as columns, retail and 10+ wholesale prices, customer/phone/date lines); printed or saved as PDF from the browser.
- **Import / export** — reads Maftuna's workspace product records read-only (with provenance hashes); exports CSV prices, a full JSON backup, and a ZIP in the workspace schema (1.3).
- **Public feed** — `GET /api/public/catalogue?locale=uz|ru|en`: active products and published collections only.

## Run it (dev)

```bash
cd engineering/codebase/ocharo-catalogue.backend-services && dotnet run --project OcharoCatalogue.Api --launch-profile https
```

```bash
cd engineering/codebase/ocharo-catalogue.frontend-services && pnpm install --frozen-lockfile && pnpm dev
```

- API `https://localhost:8278` / `http://localhost:8279`; Vite `https://localhost:8282` (proxies `/api`).
- Single host: `pnpm build && pnpm run deploy` copies the SPA into the API's `wwwroot/` (ignored by git).
- First boot imports `Catalogue:SourceRoot` when the catalogue is empty (Development points at Maftuna's `portfolio/products`).
- Runtime data (SQLite + blobs) lives in `OcharoCatalogue.Api/App_Data/` and is never committed.

## Verify

```bash
cd engineering/codebase/ocharo-catalogue.backend-services && dotnet test OcharoCatalogue.BackendServices.slnx
```

```bash
cd engineering/codebase/ocharo-catalogue.frontend-services/apps/web && pnpm typecheck && pnpm lint && pnpm test
```

- Real-data import check (read-only): set `OCHARO_WORKSPACE_PRODUCTS` to the products folder before `dotnet test`.

## Pointers

- Product: [product/product.md](../../../product/catalogue/product.md) · state: [product/context.md](../../../product/catalogue/context.md)
- Architecture: [engineering/architecture/architecture.md](../../architecture/catalogue/architecture.md)
- Track: [engineering/research/previous-work/planning.md](../previous-work/catalogue/planning.md)
- Sibling platforms: `ventures.ocharo-brand` (logo + links), `ventures.ocharo-marketing` (posts + social automation), `ventures.ocharo-blog` (blog), `ventures.ocharo-studio` (3D studio)
- Conventions: `wow-two-ws/conventions/`

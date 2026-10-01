> Adopted source reference: Ocharo Platform owns this companion. Shared runtime integration is deferred.

# Ocharo Brand

One place for the Ocharo logo, colours, type and links. Maftuna publishes a brand release here; Catalogue, Blog and the other Ocharo platforms read it from stable URLs, so a new logo reaches every app without copying files.

## What it does

- **Logos** — six slots (master, on-light, on-dark, avatar, favicon, social); every upload is a content-addressed version (SHA-256 + WebP preview) with draft/approved status; pick the current version per slot.
- **Identity** — name and spelling rule, descriptor/tagline/location in uz/ru/en, palette (key, hex, role), typography, public links, voice notes.
- **Releases** — publishing snapshots identity + current logos as release N (with a change list against the previous one); consumers only ever see published releases; the draft keeps its own history.
- **Appearances (Tadbirlar)** — time-boxed overrides (a holiday logo, an accent colour, an event badge link beside the logo) up to 92 days, no overlaps, cancel any time; the public kit swaps them in and out on schedule.
- **Public kit** — JSON, pinned releases, stable logo URLs, immutable media and CSS tokens (see Contracts in the architecture doc).
- **Ulanish** — stable logo URLs, data/style endpoints and sample code; the overview lists which platforms read the kit and which release each last saw.

## Run it (dev)

```bash
cd engineering/codebase/ocharo-brand.backend-services && dotnet run --project OcharoBrand.Api --launch-profile https
```

```bash
cd engineering/codebase/ocharo-brand.frontend-services && pnpm install --frozen-lockfile && pnpm dev
```

- API `https://localhost:8286` / `http://localhost:8287`; Vite `https://localhost:8288` (proxies `/api`).
- Single host: `pnpm build && pnpm run deploy` copies the SPA into the API's `wwwroot/` (ignored by git).
- First boot seeds the identity from Maftuna's brand guidelines and, when `Brand:SeedLogoRoot` exists (Development points at her `portfolio/brand/logo`), the three logo files, then publishes release 1.
- Runtime data (SQLite + blobs) lives in `OcharoBrand.Api/App_Data/` and is never committed.

## Verify

```bash
cd engineering/codebase/ocharo-brand.backend-services && dotnet test OcharoBrand.BackendServices.slnx
```

```bash
cd engineering/codebase/ocharo-brand.frontend-services/apps/web && pnpm typecheck && pnpm lint && pnpm test
```

## Pointers

- Product: [product/product.md](../../../product/brand/product.md) · state: [product/context.md](../../../product/brand/context.md)
- Architecture + contracts: [engineering/architecture/architecture.md](../../architecture/brand/architecture.md)
- Track: [engineering/research/previous-work/planning.md](../previous-work/brand/planning.md)
- Consumers: `ventures.ocharo-catalogue` (print sheets), `ventures.ocharo-blog` (site header/footer, export), all platforms' sidebar switcher
- Conventions: `wow-two-ws/conventions/`

# ventures.ocharo-brand — Architecture

*Last updated: 2026-10-21*

```mermaid
flowchart LR
  WS[Maftuna workspace<br/>portfolio/brand/logo] -->|read-only seed, SHA-256| API
  UI[Vue operator app<br/>/] --> API[OcharoBrand.Api]
  API --> DB[(SQLite<br/>draft + revisions, logo versions,<br/>releases, appearances, consumers)]
  API --> Blobs[(Blob store<br/>originals + WebP previews)]
  API -->|/api/public/brand| CAT[Ocharo Catalogue<br/>print sheets]
  API -->|/api/public/brand| BLOG[Ocharo Blog<br/>header, footer, export]
```

| Project | Responsibility |
|---|---|
| `OcharoBrand.Api` | Admin + public controllers, runtime config and system status, local-pilot boundary (loopback, no cross-origin writes, CORS on `/api/public`), ProblemDetails mapping, seed-on-empty |
| `OcharoBrand.Application` | `BrandService` (identity, logo versions, publish with diff, appearances), `PublicBrandService` (kit build, logo fallbacks, tokens CSS, consumer tracking) |
| `OcharoBrand.Domain` | `BrandDraft`, `BrandIdentity` (localized text, palette, typography, links), `LogoVersion`, `BrandRelease`, `BrandAppearance`, records |
| `OcharoBrand.Infrastructure` | `MediaStore` (kit `IBlobRepository`, SkiaSharp WebP previews), `BrandSeeder` |
| `OcharoBrand.Persistence` | EF Core SQLite; draft saves check the expected revision and write a snapshot |

## Contracts

- `GET /api/public/brand?locale=uz|ru|en&app={name}` — latest release plus any active appearance: `{ name, spellingRule, release, publishedAt, locale, descriptor, tagline, location, descriptors, palette[], typography, links[], logos{slot: {url, fileUrl, previewUrl, contentType, width, height, sha256, fromAppearance}}, appearance }`; cached 60 s.
- Cache lifetimes of the kit, stable logo URLs and tokens are capped at the next appearance start/end, so no cache outlives a scheduled change.
- `GET /api/public/brand/releases/{n}` — a pinned release (no appearance applied).
- `GET /api/public/brand/logo/{slot}[?preview=true]` — stable logo URL that always serves the current file (5-min cache + ETag).
- `GET /api/public/brand/media/{sha}[/preview]` — immutable, content-addressed files (1-year cache), served only once disclosed: part of a published release or of an appearance that has started. Draft uploads and future appearance artwork return 404 publicly (a hash is not confidentiality); the admin app reads them through `/api/brand/media/{sha}`.
- `GET /api/public/brand/tokens.css` — `--ocharo-{key}` colour variables plus font stacks.
- Consumers identify themselves with `X-Ocharo-App` or `?app=`; the overview lists them with the release they last read.
- Editing API under `/api/brand/*` is loopback-only and revision-checked (`expectedRevision`, 409 on conflict).
- `GET /api/runtime-config` — anonymous, `no-store`: the suite's `OCHARO_*_URL` app links from the optional `OcharoApps:*`
  settings (Desk, Catalogue, Studio, Marketing, Editorial, Brand, Atlas, Wear, Blog). Only absolute HTTPS values without
  credentials, query or fragment are served; startup logs one warning naming every rejected setting.
- `GET /api/system/status` — anonymous, `no-store`: `{ service, status: "live", version }`. `GET /api/system/version`
  stays for existing callers. Both, and runtime config, are exempt from the Ocharo SSO boundary; with SSO off they stay
  behind the loopback boundary like every other route.

## Operator frame

The SPA wears the Ocharo suite frame, as Desk and Catalogue do
([suite agreement](../../../../../docs/architecture.md#suite-frame-and-app-links)):

- `bootstrap/AppLayout.vue`, mounted through a layout route, holds every place; Brand has no public SPA route.
- Top bar: the SDK `AppSwitcherMenu` (current `brand`, the "O" mark), the app version, the breadcrumb from route
  metadata, the release the draft stands on with Refresh, and the SDK `AccountMenu` last — appearance, the Brand and API
  versions, and with an Ocharo session the Desk account item, identity and sign-out.
- Sidebar (SDK `SidebarMenu`, rail state in `ocharo.brand.sidebar-collapsed`): Overview, Identity, Logos and Appearances,
  then Connect under Admin. Phones get the SDK `BottomNavMenu` with the first four and a full-screen More dialog.
- App links resolve runtime config first, then build-time `VITE_OCHARO_*_URL`, then the local-run defaults; a malformed
  served value drops that app and raises one toast. Brand reads its own kit same-origin and needs no other address.
- Colour: the SDK `ocharo` theme through `ColorModeProvider` and the generated pre-paint `/theme.js`, key
  `ocharo.brand.theme`. The logo proof surfaces (`--color-artwork-*`: paper `#f8f7f3`, ink `#20211e`, transparency check
  `#e6e2d8`/`#fffdf8`) and palette swatches are artwork colours and never follow the interface mode.
- Language: frame copy uses English message keys with Uzbek and Russian entries; the interface stays Uzbek because the
  pages are still Uzbek literals, so the account menu shows no language rows until the pages are localized.

## Consumers

- Catalogue print sheets fetch the kit in the browser (logo file + links; catalogue contact lines that repeat a brand handle are dropped).
- Blog fetches the kit on the server (`Blog:BrandBaseUrl`), copies the logo into its own media store by SHA-256 and uses the brand links; an unreachable Brand service keeps the blog's own settings.

## SDK usage and extraction candidates

- Uses the published kit `WoW2.Sdk.Beta.Be 10.0.71-beta` (blob storage, mediator registration, validated settings) and `@wow-two-beta/fe 0.0.28` (HTTP client, config, router, error boundary, icons, colour mode, app shell, app switcher, account menu, sidebar and bottom nav).
- Candidates for SDK extraction (shared with Catalogue/Blog): revisioned document store, content-addressed media store with previews, local-pilot boundary middleware, a generic "published kit" client with cache + fallback.

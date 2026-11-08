# Files adoption

*Last updated: 2026-11-08*

Ocharo's media moves onto WoW2 Files: tenant `ocharo` on the shared files host B first, Ocharo's own instance later by
a tenant move. This is the platform lane's track of the Files integration goal; the cross-lane track is the Files repo's
`engineering/planning/files-integration.md`, cited as `files-integration #N`. Sources: Files `product/product.md`
§ Ocharo slice (P1) and § Phases, Files research `t1-services-tenancy.md`, `t8-identity-contracts.md` and
`t9-wheelhouse-migration.md`, and `ocharo-ws` `docs/repository-review/creative-assets.md`.

Code links pin `ocharo-platform` `b2388af`, `ocharo-marketing` `15377b7` and `ocharo-studio` `271bd63`. Sizes come from
code limits and local dev data; nothing read prod.

## Status

- M1 picks written 2026-11-08 (`files-integration` #27, #30, #32). The developer reviews the built result at the end
  instead of confirming M1 (developer, 2026-11-08), so derivable picks below carry their reason and stand until then.
- M2 started 2026-11-08: the registry entries (#18) and roles (#19) are in `engineering/deployment/deploy.yml`, and the
  migration manifest (#20) is written. App code waits on the rest of #17: Files' `Client` package and B's dev
  deployment first.

## Instance

- #1 dev: tenant `ocharo` on B's dev deployment, served under Ocharo's own dev hostname, one bucket per environment
  (Files `product.md` § Ocharo slice, t9 W7). B is WoW2's own instance, the first shared host (Files `product.md`
  § Shared files host).
- #2 prod stays parked with Ocharo prod (`files-integration` Goal, out of scope); its tenant and bucket follow dev's shape.
- #3 Ocharo's own instance comes later by a tenant move (t1 #4): its hostname moves by CNAME, so file ids, references and
  the Identity resource entry (#18) stay unchanged.

## Media

- #4 what each app keeps:

| App | Repo | Kinds | Bytes now | Local dev | Public |
|---|---|---|---|---|---|
| Brand | platform | 6 logo slots, PNG/JPEG/WebP, a 1200 px WebP preview each | [`MediaStore`](https://github.com/ocharo-hq/ocharo-platform/blob/b2388af91a50be3e4f6fd706a4832af88a2361a8/engineering/codebase/ocharo-brand.backend-services/OcharoBrand.Infrastructure/Brand/MediaStore.cs#L14-L91) over `IBlobRepository`: `blobs/{media,previews}/{sha[..2]}/{sha}` on `brand-data`, SQLite `media_blobs` index | 3 originals, 2.6 MiB | disclosed logos under `/api/public/brand/*`, immutable |
| Catalogue | platform | product images in 6 roles with a 900 px preview; release PDFs and page PNGs | images as Brand on `catalogue-data`; releases in SQLite [`catalogue_release_assets`](https://github.com/ocharo-hq/ocharo-platform/blob/b2388af91a50be3e4f6fd706a4832af88a2361a8/engineering/codebase/ocharo-catalogue.backend-services/OcharoCatalogue.Persistence/Catalogue/CatalogueDesignSchema.cs#L17) | 160 originals, 367 MiB, plus 160 previews, 9.4 MiB; releases under 0.5 MiB | active products' images, released editions |
| Blog (Editorial) | marketing | covers, site logo, hero and band, edited derivatives with receipts, a 1400 px preview | [`MediaStore`](https://github.com/ocharo-hq/ocharo-marketing/blob/15377b74efbc2ad4ec12d6bec78a745731746ad2/engineering/codebase/ocharo-blog.backend-services/OcharoBlog.Infrastructure/Blog/MediaStore.cs#L15-L57) as Brand, on `blog-data` | 4 originals, 7.2 MiB | site images and published covers, immutable |
| Marketing | marketing | uploads in 6 roles, card PNG renders, AI images, draft export ZIPs | [`LocalAssetService`](https://github.com/ocharo-hq/ocharo-marketing/blob/15377b74efbc2ad4ec12d6bec78a745731746ad2/engineering/codebase/ocharo-marketing.backend-services/OcharoMarketing.Infrastructure/Marketing/Services/LocalAssetService.cs#L14-L110) over [`AddLocalBlobStorage`](https://github.com/ocharo-hq/ocharo-marketing/blob/15377b74efbc2ad4ec12d6bec78a745731746ad2/engineering/codebase/ocharo-marketing.backend-services/OcharoMarketing.Api/Configurations/HostConfiguration.Extensions.cs#L80): `originals/{id}/{sha}`, `exports/{draft}/{revision}.zip`, SQLite `Assets.BlobPath` | 16 files, 12 MiB | none; Instagram and WhatsApp delivery need a public URL nothing sets |
| Desk | platform | preparation images (5 MiB), export ZIPs (27 MiB), commerce images (15 MiB) and edition PDFs (64 MiB) with page PNGs, sourcing images, PDFs and MP4s (64 MiB) with a 480 px WebP thumbnail | 5 `bytea` columns, each row keyed by SHA-256: [`preparation.media`](https://github.com/ocharo-hq/ocharo-platform/blob/b2388af91a50be3e4f6fd706a4832af88a2361a8/engineering/codebase/brand-workspace.backend-services/BrandWorkspace.Persistence/Migrations/003-preparation/Apply.sql#L16), [`preparation.exports`](https://github.com/ocharo-hq/ocharo-platform/blob/b2388af91a50be3e4f6fd706a4832af88a2361a8/engineering/codebase/brand-workspace.backend-services/BrandWorkspace.Persistence/Migrations/003-preparation/Apply.sql#L99), [`commerce.media`](https://github.com/ocharo-hq/ocharo-platform/blob/b2388af91a50be3e4f6fd706a4832af88a2361a8/engineering/codebase/brand-workspace.backend-services/BrandWorkspace.Persistence/Migrations/005-commerce/Apply.sql#L25), [`sourcing.media_contents`](https://github.com/ocharo-hq/ocharo-platform/blob/b2388af91a50be3e4f6fd706a4832af88a2361a8/engineering/codebase/brand-workspace.backend-services/BrandWorkspace.Persistence/Migrations/007-sourcing/Apply.sql#L69-L70) `bytes` and `thumbnail` | code gives limits only; ~260 MiB of sourcing media (`ocharo-ws` `docs/roadmap.md` § Landings) | published-release media and approved edition PDFs and PNGs |
| Studio | studio | saved-look thumbnails, 240 px WebP data URLs | SQLite `studio_workspaces.state_json`, [1 MiB each](https://github.com/ocharo-hq/ocharo-studio/blob/271bd6319f1d1e52701d9a3ad334043acc9735e1/engineering/codebase/ocharo-studio.backend-services/OcharoStudio.Application/Studio/State/StudioStateValidator.cs#L13), 4 MiB a state | database 0.9 MiB | private |
| Assets | assets | 7 garment GLBs (~5.2 MB), the `.blend` master, a release manifest by SHA-256 | Git LFS and a private GitHub release; Studio keeps copies in `public/assets/models` | 9 LFS files | the GLBs, through Studio |

- #5 Desk never stores image-worker output: background removal is bytes in, PNG out, through
  `POST /api/integrations/media/background-removal`, so nothing there moves.
- #6 every app already keys media by SHA-256; only Marketing's ids are GUIDs, with the hash inside the blob path.

## Order

- #7 waves, dev first, each through the Wheelhouse migration module (t9 #5):
  1. Brand, then Catalogue (this repo). Brand's 3 logos pilot the path end to end. Catalogue holds the most pilot bytes
     (367 MiB locally), so "smallest" in Files `product.md` § Phases #4 holds for code surface, not bytes.
  2. Blog, Marketing and Studio (`ocharo-marketing`, `ocharo-studio`). Studio needs no migration job (#12).
  3. Desk (this repo), last: 5 `bytea` columns, the largest store, immutable rows.
  4. Assets releases and garment models (`ocharo-assets`, `ocharo-studio`), after Desk (#14).
- #8 a wave starts after the one before it reads back on dev: count and checksum parity, sample downloads (t9 #5 step 7).

## Delivery

- #9 every app keeps its media URLs and its own public-or-private decision (`CatalogueMediaAccessService`,
  `PublicBrandService.IsDisclosedAsync`, `EditorialAccessMiddleware`, Desk's release checks); the route answers with a
  redirect to Files. Reason: hashes inside product, release and article JSON stay valid, and no snapshot is rewritten.
  - private media redirects to a short-lived signed `/f` URL.
  - disclosed media redirects to Files' public `/m` URL on Ocharo's hostname, cacheable; Marketing's social delivery
    reads the same URL.
- #10 previews and thumbnails become Files image variants at each app's current width (Catalogue 900, Brand 1200,
  Blog 1400, Desk sourcing 480 px, all WebP); the migration copies originals only. Reason: Files `product.md` says the
  apps drop their `MediaStore` copies, and the 3 preview pipelines are those copies.
- #11 references replace copies (Files `product.md` § Instances per owner):
  - Blog references Brand's published logo file instead of copying it on a public page render
    ([`SiteService.BrandedAsync`](https://github.com/ocharo-hq/ocharo-marketing/blob/15377b74efbc2ad4ec12d6bec78a745731746ad2/engineering/codebase/ocharo-blog.backend-services/OcharoBlog.Application/Blog/Site/SiteService.cs#L53-L63)),
    which ends an anonymous GET writing a file.
  - Desk's commerce attach references the preparation file instead of copying its bytes into `commerce.media`.
- #12 Studio thumbnails: a new save uploads the thumbnail to Files and keeps its file id; an existing data URL stays
  valid until that look is saved again. Reason: a thumbnail is a regenerable 240 px snapshot, so no migration source
  is needed.
- #13 Desk: a forward-only migration (the next free number; `011-identity-cutover` is reserved) adds `file_id` beside
  each `bytes` column and drops `NOT NULL` from it. `preparation.media` and `preparation.exports` are immutable by
  trigger, so the ids fill by copy-and-repoint; t9's retire step drops the `bytes` columns later. Export ZIPs become
  private files and keep `(organization, idempotency_key)` and `request_hash`.
- #14 Assets releases: `release.mjs` uploads each asset as a public file by content hash, and Studio's
  `sync-garments.mjs` fetches by hash and checks the manifest. The private GitHub release stays until then, as
  `creative-assets.md` allows either.

## Callers

- #15 rule (`files-integration` #1, #4): client credentials when the app acts as itself, token exchange when it acts for
  a person; long work a person started runs as the app once accepted. The be sdk has both callers:
  `AddOAuth2ClientCredentials` and `AddOAuth2TokenExchange`, with `AddIdentityBff` reading the subject token from the
  session (Identity `product/features/service-tokens.md` § Caller side).

| Client | Index | For a person: token exchange | As itself: client credentials |
|---|---|---|---|
| `desk` | 0 | operator uploads, exports, edition uploads, commerce attach, member reads | the `--import-sourcing` CLI, public-media redirects |
| `brand` | 1 | logo uploads, member reads | the boot seeder, public-media redirects |
| `catalogue` | 2 | product media, design prepare, workspace import, member reads | the boot seed, public-media redirects |
| `marketing` | 3 | uploads, Studio creative imports, member reads | `GenerationWorker`'s card PNGs and AI images, the seed import |
| `editorial` | 4 | uploads, edits, research import, member reads | `BlogSeeder`, public-media redirects |
| `studio` | 5 | saved-look thumbnails | none |
| `public-build` | 6 | none | none: published media is public (#9) |
| `assets-release` | 7, new | none | release uploads from `ocharo-assets` CI (#14) |

- #16 scopes: the `files` resource lists Files' P1 scopes `files.read`, `files.write`, `files.share` and `files.admin`
  (files lane, 2026-11-08); `files.tenants` stays B's operator scope and never registers here. Every app client gets
  `files.read` and `files.write`, and `assets-release` `files.write`. No client gets `files.share` (no Ocharo share in
  P1) or `files.admin` (the mover uses Files-minted import tokens, t9 W6); `files.delegate` is gone
  (`files-integration` #12).

## Registrations

`files-integration` #32: Ocharo's Identity registry is this repo's `engineering/deployment/deploy.yml`, service
`identity`; Marketing and Studio register nothing and link with `from: { repository: platform, key: … }`.

- #17 inputs M2 waits on:
  - files lane: Files' `Client` package; variants (#10 here); public `/m` delivery on a tenant hostname (#9);
    references across one tenant's apps (#11); a `sqlite-column` source kind for Catalogue's release assets (#20).
    The resource contract (`files-integration` #10) and the person rule (#19) are answered.
  - wheelhouse lane: B's dev deployment with tenant `ocharo`, Ocharo's dev hostname and the owner keys of #18; the
    migration module.
  - a writer for `ocharo-marketing`, `ocharo-studio` and `ocharo-assets`: once wave 1 reads back on dev, the
    orchestrator proposes one lane per repo, which the developer starts (`files-integration` #42); this lane keeps
    writing only `ocharo-platform`.
- #22 B trusts only Ocharo's Identity in P1: no WoW2 Identity joins this goal (developer, 2026-11-08,
  `files-integration` #29).
- #18 this repo writes the entries, in `deploy.yml` since 2026-11-08; the wheelhouse lane types the per-environment
  owner keys, `Resources:2:Uri` as `https://files.{domain}/` and `Resources:2:SignalUrl` as `{Uri}signals/identity`,
  and each caller's Files base URL (B is no Ocharo site, so no `from:` reaches it), on target `ocharo-dev`, plus the
  same Uri as tenant `ocharo`'s audience on `files-dev` (wheelhouse lane, 2026-11-08). The shape, as written:

```yaml
Identity:Server:Resources:2:Name: { default: files }
Identity:Server:Resources:2:Uri: { type: url, required: true }        # `{Ocharo's Files hostname}/`, with its slash
Identity:Server:Resources:2:Scopes:0: { default: files.read }
Identity:Server:Resources:2:Scopes:1: { default: files.write }
Identity:Server:Resources:2:Scopes:2: { default: files.share }
Identity:Server:Resources:2:Scopes:3: { default: files.admin }
Identity:Server:Resources:2:PermissionNamespace: { default: files }
Identity:Server:Resources:2:SignalUrl: { type: url, required: true }  # `{Uri}signals/identity`
# per caller client N; `authorization_code` goes first on a client that signs people in, since a Grants list replaces
# "empty means the code flow alone"
Identity:Server:Clients:N:Grants:0: { default: authorization_code }
Identity:Server:Clients:N:Grants:1: { default: client_credentials }   # only clients with an as-itself call (#15)
Identity:Server:Clients:N:Grants:2: { default: token_exchange }
Identity:Server:Clients:N:Resources:0: { default: files }             # as itself
Identity:Server:Clients:N:ExchangeResources:0: { default: files }     # for a person
Identity:Server:Clients:N:Scopes:0: { default: files.read }
Identity:Server:Clients:N:Scopes:1: { default: files.write }
```

  - `Uri` and `SignalUrl` are required, so the first dev deploy of a release carrying them needs both typed; their
    values follow from Ocharo's dev domain, so B need not run yet. A missing or malformed one fails Identity's
    `config check` and stops the deploy while the running release keeps serving.
  - clients 0–5 reuse their existing generated secrets; client 4 appends to its `desk` entries; Studio (5) has token
    exchange only.
  - `assets-release` gets a generated secret that `ocharo-assets` CI must hold; `public-build`'s secret has the same
    gap today (`deployment.md`), so its holder is settled with wave 4.
- #19 roles: Files reads only `org_permission` (`files-integration` #23, files lane D2 (a), 2026-11-08). Owner and admin
  hold `files:read`, `files:write`, `files:share` and `files:admin`; editors `files:read` and `files:write`, since
  shares and Files admin sit with the roles that approve elsewhere. Viewer and billing grant none, as in every app.

## Migration

- #20 manifests (t9 #4) list each wave's sources; this repo's
  [`files-migration.yml`](../deployment/files-migration.yml) holds Brand, Catalogue and Desk (written 2026-11-08, adding
  a composite `key` and a `wave` to t9's draft), `ocharo-marketing`'s holds Blog and Marketing:
  - Brand: `volume-folder`, `brand-data`, `blobs/media`, reference `brand.media:{sha256}`.
  - Catalogue: `volume-folder`, `catalogue-data`, `blobs/media`; `catalogue_release_assets` needs `sqlite-column`,
    which t9 lists after P1, so it is asked of the files lane, and those assets stay in SQLite until it lands.
  - Desk: 4 `postgres-column` sources (`preparation.media`, `preparation.exports`, `commerce.media`,
    `sourcing.media_contents`); thumbnails regenerate as variants (#10).
- #21 each app's cutover build fills its file ids from Files' id map (t9 #5 step 6); source bytes stay until t9's
  separate retire job.

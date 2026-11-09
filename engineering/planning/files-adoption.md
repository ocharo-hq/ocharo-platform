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
  migration manifest (#20) is written.
- Wave 1, Brand built 2026-11-09 (#23): it stores in Files and redirects to Files links, with 49 tests passing on an
  in-memory broker set; platform `v0.1.29` carries it. Not yet run against a real Files: dev holds no logo to move, so
  its read-back is a live upload (#25).
- Wave 1, Catalogue's product images built 2026-11-09 (#24), the same way, 91 tests passing.
- Wave 1 read back on dev 2026-11-09 by live uploads, since dev held no media to move (#25).
- Wave 1, Catalogue's released PDFs and page PNGs built 2026-11-09 (#26), 93 tests passing; it read back on dev the same
  day (release `wave1-check`), which closed wave 1.
- Wave 2 built and pushed 2026-11-09 (#27–#30): Blog and Editorial (329 tests), Marketing (368) and Studio (88); all
  three link `Files:BaseUrl` to this repo's `Identity:Server:Resources:2:Uri`, so nobody types it.
- Wave 2 read back on dev 2026-11-09 (marketing `v0.1.8`, studio `v0.1.12`): Blog's logo and preview, an Editorial
  cover, a Marketing asset and a Studio thumbnail each redirect to Files and serve their bytes; `marketing-assets` held
  3 files on dev, which move and fill their ids on Marketing's next boot. The read-back found 240×34 Studio thumbnails
  from a short viewport: studio `d1622c6` frames them at 240×180 from the viewport's centre.
- Wave 3, Desk, released as `v0.1.34` 2026-11-09 (#32), 305 tests passing; on dev, preparation and sourcing read back
  from Files the same day (the thumbnail as the 480×360 variant); export download and commerce wait for the final check.
- Wave 4, Assets and Studio's garment sync, built and pushed 2026-11-09 (#33); its first upload waits for client 7's
  secret on `identity` and in `ocharo-assets`' GitHub secrets.

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
  1. Brand, then Catalogue (this repo). Brand's logos pilot the path end to end; dev holds none, so there a live
     upload does (#25). Catalogue holds the most pilot bytes
     (367 MiB locally), so "smallest" in Files `product.md` § Phases #4 holds for code surface, not bytes.
  2. Blog, Marketing and Studio (`ocharo-marketing`, `ocharo-studio`). Studio needs no migration job (#12).
  3. Desk (this repo), last: 5 `bytea` columns, the largest store, immutable rows.
  4. Assets releases and garment models (`ocharo-assets`, `ocharo-studio`), after Desk (#14).
- #8 a wave starts after the one before it reads back on dev: count and checksum parity, sample downloads (t9 #5 step 7).

## Delivery

- #9 every app keeps its media URLs and its own public-or-private decision (`CatalogueMediaAccessService`,
  `PublicBrandService.IsDisclosedAsync`, `EditorialAccessMiddleware`, Desk's release checks); the route answers with a
  redirect to Files. Reason: hashes inside product, release and article JSON stay valid, and no snapshot is rewritten.
  - private and disclosed media alike redirect to a signed `/f` link, valid 15 minutes, with the redirect cached at
    most 5. Reason: disclosure changes with appearances and withdrawals, and a link keeps that decision in the app
    with no visibility to keep in step (refined 2026-11-09, building Brand).
  - Files' public `/m/{sha}` and `/m/{sha}.webp` (files lane, 2026-11-08) wait for a consumer that needs a stable
    public URL: Marketing's social delivery, in wave 2.
- #10 previews and thumbnails become Files image variants at each app's current width (Catalogue 900, Brand 1200,
  Blog 1400, Desk sourcing 480 px, all WebP); the migration copies originals only. Reason: Files `product.md` says the
  apps drop their `MediaStore` copies, and the 3 preview pipelines are those copies. Built in Files `6310647`:
  `Files:DefaultPolicy:Variants` gives every app those widths, and a link's `Width` picks the narrowest variant at least
  that wide, else the widest.
- #11 references replace copies (Files `product.md` § Instances per owner), checked per tenant only (files lane,
  2026-11-08):
  - Blog references Brand's published logo file instead of copying it on a public page render
    ([`SiteService.BrandedAsync`](https://github.com/ocharo-hq/ocharo-marketing/blob/15377b74efbc2ad4ec12d6bec78a745731746ad2/engineering/codebase/ocharo-blog.backend-services/OcharoBlog.Application/Blog/Site/SiteService.cs#L53-L63)),
    which ends an anonymous GET writing a file (built, #31).
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
  - files lane: all answered by 2026-11-08: the resource contract (`files-integration` #10), the person rule (#19),
    variants (#10), public `/m` delivery (#9), references across one tenant's apps (#11), `sqlite-column` (#20), and
    `Client` `10.0.5-beta` with image `v0.1.11` published.
  - wheelhouse lane: B's dev deployment with tenant `ocharo`, Ocharo's dev hostname and the owner keys of #18; the
    migration module.
  - a writer for `ocharo-marketing`, `ocharo-studio` and `ocharo-assets`: once wave 1 reads back on dev, the
    orchestrator proposes one lane per repo, which the developer starts (`files-integration` #42); this lane keeps
    writing only `ocharo-platform`.
- #22 B trusts only Ocharo's Identity in P1: no WoW2 Identity joins this goal (developer, 2026-11-08,
  `files-integration` #29).
- #18 this repo writes the entries, in `deploy.yml` since 2026-11-08; the wheelhouse lane types the per-environment
  owner keys, `Resources:2:Uri` as Ocharo's Files host (dev: `https://ocharo-files-dev.{products host}.sslip.io/`, since dev has no domain) and `Resources:2:SignalUrl` as `{Uri}signals/identity`,
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
  shares and Files admin sit with the roles that approve elsewhere. Viewer holds `files:read` (2026-11-09): a viewer
  reads every app's media as a member, and that read reaches Files with the viewer's exchanged token. Billing holds none.

## Migration

- #20 manifests (t9 #4) list each wave's sources; this repo's
  [`files-migration.yml`](../deployment/files-migration.yml) holds Brand, Catalogue and Desk (written 2026-11-08, adding
  a composite `key` and a `wave` to t9's draft), `ocharo-marketing`'s holds Blog and Marketing:
  - Brand: `volume-folder`, `brand-data`, `blobs/media`, reference `brand.media:{sha256}`.
  - Catalogue: `volume-folder`, `catalogue-data`, `blobs/media`; `catalogue_release_assets` uses `sqlite-column`, which
    the mover reads in P1, opening `{root}/{database}` read-only (files lane, 2026-11-08).
  - Desk: 4 `postgres-column` sources (`preparation.media`, `preparation.exports`, `commerce.media`,
    `sourcing.media_contents`); thumbnails regenerate as variants (#10).
- #21 each app's cutover build fills its file ids from Files' id map (t9 #5 step 6); source bytes stay until t9's
  separate retire job. An imported file's app is its reference prefix (`brand`, `catalogue`, `desk`), the same as
  Ocharo's client ids, so imports and new uploads share one policy (files lane, 2026-11-08).

## Build

- #23 Brand, wave 1 (2026-11-09):
  - packages: core `10.0.105-beta`, `Identity.Client` `10.0.30-beta` (the BFF fix of `identity-cutover` #54), Files'
    `Abstractions` and `Client` `10.0.5-beta`.
  - `Files:BaseUrl` set: `FilesMediaStore` uploads each original once by SHA-256 as app `brand`, with idempotency key and
    reference `brand.media:{sha}`; each media route redirects to a Files link, the preview at width 1200. The Files
    client takes Brand's own Identity client: a person's token exchanged within their session, Brand's own otherwise.
  - `media_blobs` gains `FileId`; boot fills it for files stored before Files by matching SHA-256 against Brand's files
    in Files. That is the cutover's id-map step, read with Brand's own token instead of the import's.
  - a file with no id yet reads its local bytes, so a deploy before the move keeps every logo; the local `MediaStore`
    also serves a run with `Files:BaseUrl` unset, and both leave at t9's retire step, once local runs use a Files too.
  - an unreachable Files at boot leaves ids unfilled and the boot running; an upload then answers `503`.
  - hosted settings: `Files:BaseUrl` is a required owner key on `brand`, typed as that same Uri.
  - not run: a real Files round trip, Brand on dev; dev holds no logo to move (#25).
  - unverified: a consumer that `fetch()`es a Brand logo across origins now follows a redirect to the Files host, whose
    `/f` answer may carry no `Access-Control-Allow-Origin`; every caller seen uses `<img>` or a server-side download.
- #24 Catalogue's product images, wave 1 (2026-11-09):
  - the same packages, store, backfill, local fallback and settings as #23, as app `catalogue` with reference
    `catalogue.media:{sha}` and previews at width 900; the media route keeps `public, no-cache` and `private, no-store`,
    so a redirect is asked again each time and the route keeps deciding visibility.
  - the release check (`CatalogueReleaseService.VerifyMediaAsync`) asks the store: a Files image must be there, `Ready`
    and of the same SHA-256, since Files hashed it on upload; a local one is hashed as before.
  - released PDFs and page PNGs stayed in `catalogue_release_assets` in SQLite; #26 moves them.
  - not run: a real Files round trip, Catalogue on dev; dev holds no image to move (#25).
- #25 dev holds no media stored before Files (2026-11-09, the wheelhouse lane's `brand-media` plan):
  - the seeds read only a machine's gitignored `appsettings.Local.json` (`Brand:SeedLogoRoot`, `Catalogue:SourceRoot`),
    and neither image carries a seed folder; dev's Brand seeded a draft with no logo and published nothing, so
    `/api/public/brand/logo/social` answers `404`, and dev's Catalogue seeded no product.
  - `brand-data` and `catalogue-data` hold no `blobs/` folder, since the blob store makes it on the first local write, and
    the Files-backed builds (`v0.1.29`, `v0.1.31`) never write one; the mover refuses a source folder that does not exist.
  - the 3 logos and 160 images in #4 are this Mac's review runtime, not dev.
  - so wave 1 moves nothing on dev: Wheelhouse runs no `brand-media` or `catalogue-media` job there, and
    `catalogue-release-assets` waits for Catalogue's release-asset step (#24).
  - the manifest stays as written, for a target that stored blobs before Files.
  - wave 1's dev read-back (#8) is a live upload instead, by a signed-in Ocharo owner:
    - Brand: upload a master logo and publish. Anonymous `GET {brand}/api/public/brand/logo/social` answers `302` to the
      Files host, since `social` falls back to `master`; the link serves the uploaded file's SHA-256.
    - Catalogue: add a product image. A member's `GET {catalogue}/api/catalogue/media/{sha}` answers `302`; the link
      serves the same SHA-256.
  - passed on dev 2026-11-09 (orchestrator; Files track doc `81c0b1d`):
    - Brand: the owner uploaded a master logo and published release 1. Anonymous `logo/social` and `logo/master` answer
      `302` to the dev Files host, and the link serves the uploaded SHA-256.
    - Catalogue: test product `nimcha-v01` with one image (144803 B, 1600×1200). A member's `media/{sha}` answers `302`
      to Files, and the link serves the uploaded bytes.
    - fixed outside this repo on the way: the trailing `/` of Files' trusted issuer (wheelhouse), and Files `v0.1.15`'s
      dev settings, which signed links for localhost.
- #26 Catalogue's released PDFs and page PNGs, wave 1 (2026-11-09):
  - preparing a release stores each artifact in Files once per SHA-256, as app `catalogue` with idempotency key and
    reference `catalogue.release:{sha}`, named as a download saves it (`ocharo-{slug}-{locale}.pdf`,
    `ocharo-{slug}-{page}.png`); the first release with those bytes names the file.
  - SQLite keeps only the file id, in a new `catalogue_release_files` table; `catalogue_release_assets` keeps the bytes of
    artifacts prepared before Files, and of every artifact in a run with `Files:BaseUrl` unset.
  - the uploads run before the release's transaction, as the member preparing it. A refused transaction leaves them in
    Files, still referenced, and preparing again reuses them.
  - the public asset route still decides each time, with `no-store`: while the release is published, it redirects to a
    Files link valid 15 minutes, saved under the file's name; Files answers it with `Cross-Origin-Resource-Policy:
    cross-origin`, so the reader's `<img>` pages load under Catalogue's `require-corp`.
  - an artifact prepared before Files reads its SQLite bytes until boot fills its id from the files the migration moved,
    in the same pass as product images; the `catalogue-release-assets` manifest source reads only those rows.
  - not run: a release prepared on dev. Read-back: a member prepares and publishes a PDF catalogue, then an anonymous
    `GET {catalogue}/api/public/catalogue/editions/{slug}/assets/catalogue.pdf?locale=en&release={id}` answers `302` to
    Files, and the link serves the uploaded SHA-256.
- #27 Blog and Editorial, wave 2 (2026-11-09, `ocharo-marketing` `4518bad`):
  - every image through Blog's media port lands in Files once per SHA-256 as app `editorial` (Blog's Identity client),
    reference `editorial.media:{sha}`; previews are 1400 px variants; edits and static exports read originals from Files.
  - `/media/{sha}` and its preview redirect with `no-store`, since `EditorialHttpBoundaryMiddleware` forces it on
    `/media/*` and `EditorialAccessMiddleware` decides public or private each time.
  - manifest source `editorial-media` (`blog-data`, `blobs/media`), wave 2.
- #28 Marketing, wave 2 (2026-11-09, `ocharo-marketing` `bcdc6cd`):
  - assets in all 6 roles, card PNGs, AI images, Studio imports and the seed land in Files as app `marketing`,
    reference `marketing.asset:{sha}`, through a new `Assets.FileId`; export ZIPs are private files
    `marketing.export:{sha}`, and `ExportFiles` keeps each revision's first export.
  - the asset route redirects with `no-store`; the export route still answers bytes, since the desk `fetch()`es it into
    a blob and Files' `/f` sends no `Access-Control-Allow-Origin`.
  - the worker, seed and backfill call Files as Marketing; renders, ZIPs and delivery read through Files.
  - `/m` would plug into `PublicationService.ProcessNextAsync`, which leaves `SocialMediaAsset.PublicUrl` unset; it
    waits for social delivery, which is not built.
  - manifest sources `marketing-assets` (`blobs/originals`) and `marketing-exports` (`blobs/exports`), wave 2.
- #29 Studio, wave 2 (2026-11-09, `ocharo-studio` `97a2572`): a save uploads each new 240 px WebP once as app `studio`,
  reference `studio.thumbnail:{sha}`, and the state keeps `/api/studio/thumbnails/{sha}/{fileId}`, which redirects a
  member whose workspace holds it (`private, max-age=300`); data URLs saved before stay until that look is saved again.
- #30 Blog's dev site answered 500 from 07:59Z, once Brand's first release gave it a logo to decode: the x64
  `libSkiaSharp.so` links `libfontconfig.so.1`, which Blog's and Marketing's images lacked (`ocharo-marketing`
  `f6ee95e`). Both images now install `libfontconfig1`, and a build step fails when `ldd` finds a missing library.
- #31 Blog references Brand's logo file, wave 2 (2026-11-09, #11):
  - Brand's `/api/public/brand` gives each logo's `fileId` (null while it reads locally), its releases too.
  - Blog checks Brand's file is there, `Ready` and of the same SHA-256, attaches `editorial.media:{sha}` to it and keeps
    Brand's file id: no download, no upload. It copies as before when the feed has no id, Files is off, or the file
    differs.
  - anonymous visitors now get the published brand logo from Blog's media routes, which answered `401` before: the
    access check treats its SHA-256 as public, asking Brand once a minute.
- #32 Desk, wave 3 (2026-11-09):
  - every new file lands in Files once as private app `desk`, under the manifest's reference shapes (#20), which double
    as idempotency keys; new rows keep a `file_id` and no bytes.
  - `preparation.media`: `/media/{id}/content` still answers bytes, read from Files, since the design export
    `fetch()`es it. `preparation.exports`: the download redirects, saved as `manual-content-{id}.zip`.
  - `commerce.media`: the member route answers bytes (the design export reads it too); public `/media/{sha}` and edition
    assets redirect, edition files named `{slug}-{locale}-{asset}`. The attach references the preparation file (#11);
    a preparation image with no file id yet uploads once under the commerce reference.
  - `sourcing.media_contents`: content and thumbnail redirect, the thumbnail to the 480 px variant (#10); no thumbnail
    bytes are written.
  - migration `012-files` (011 stays reserved for the Identity cutover) adds `file_id`, drops `NOT NULL` from `bytes`
    and checks that a row has bytes or a file; `preparation.media` gains `byte_length`. The immutable tables never take
    an UPDATE: copy-and-repoint is an insert-only `preparation.media_files` / `export_files` record, and reads take
    `COALESCE(row.file_id, record.file_id)`.
  - the export ZIP uploads inside the export transaction, under the key and draft locks, so `(organization,
    idempotency_key)` and `request_hash` behave as before; other uploads run before their transactions.
  - boot fills legacy rows' ids from `desk` files of the same SHA-256 that carry the row's own reference, so no file
    crosses an organization or brand; the mover already skips rows with NULL content.
  - deleting sourcing media leaves its Files file referenced: a swept file would block a re-upload under the same
    idempotency key. A preparation image whose name's extension disagrees with its bytes fails Files' type check and
    stays on bytes.
  - deploy: `desk` links `Files:BaseUrl` to `Identity:Server:Resources:2:Uri`; `jobs` needs nothing.
- #33 Assets releases, wave 4 (2026-11-09, `ocharo-assets` `7f6323a`, `ocharo-studio` `2cf03ce`, registry `e59c866`):
  - client 7 `assets-release` (client credentials, resource `files`, scope `files.write`) has a typed secret, since
    the same value is `ocharo-assets`' GitHub Actions secret `OCHARO_ASSETS_CLIENT_SECRET`.
  - `release.mjs --check --upload` takes a token with `resource` = the Files root, uploads each GLB and the release
    manifest as public files (`assets-release.garment:{sha}`, `assets-release.manifest:{sha}`, idempotency key and
    reference alike), and checks each file's SHA-256, size and `ready` state; the manual `release-garments` workflow
    runs it with the variables `OCHARO_IDENTITY_URL` and `OCHARO_FILES_URL`.
  - Studio's `sync-garments.mjs --files <root>` fetches the manifest pinned in `garments-release.lock.json` and each
    GLB from `/m/{sha}`, checking size and SHA-256; the local-library mode is unchanged.
  - B serves plain `/m/{sha}` for its default tenant only, so Studio is given `{Files root}t/ocharo/`.

# Ocharo editable template audit

*Last updated: 2026-09-27 12:36 AM*

## Implement from these sources

- Catalogue: confirmed and printed **v22**, not an earlier visual study. It contains 16 pages, 29 products, and Uzbek/Russian/English editions. `car-catalogue-context.md` explicitly closes initial delivery and preserves v22 as the source baseline.
- Business card: selected **v5 Charcoal veil** in 85 × 55 mm landscape, with optional **v6 logo-only front**. The shared brand notes confirm cards were printed, but the exact printed variant/language is not recorded. Preserve labelled and logo-only choices.
- Logos: existing approved ivory master and white/black transparent PNG exports. Edit composition, dimensions, placement, tint and background; do not replace the distinctive wordmark with a font or silently redraw it.
- Source assets remain untouched. Create versioned application documents referencing imported, hashed copies. Saving an edited price/layout must not rewrite historical v22 HTML/PDF, product JSONs or card proofs.

Machine-readable read-only extraction: `/private/tmp/ocharo-editor-template-assets.json`. It includes 29 product records, copy in three languages, exact four-collage frame/focal-point geometry, card v5 spec and **58 selected raster asset records** with hashes/dimensions/provenance. Selected originals total **89,548,615 bytes**; this is a source inventory, not a recommended browser payload.

## Authoritative source paths

All paths below are relative to `/Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws`.

| Purpose | Path |
|---|---|
| Catalogue delivery status | `context/domains/external/work/career/designer/portfolio/catalogue/car-catalogue-context.md` |
| Current builder | `context/domains/external/work/career/designer/portfolio/catalogue/studies/car-build-journal-v22.py` |
| Current language copy | `context/domains/external/work/career/designer/portfolio/catalogue/studies/car-copy-v22.json` |
| Current manifest | `context/domains/external/work/career/designer/portfolio/catalogue/studies/car-journal-v22-en-manifest.json` |
| Editable HTML | `context/domains/external/work/career/designer/portfolio/catalogue/html/car-ocharo-journal-v22-{uz,ru,en}.html` |
| PDF baseline | `context/domains/external/work/career/designer/portfolio/catalogue/exports/car-ocharo-journal-v22-{uz,ru,en}.pdf` |
| Product records | `context/domains/external/work/career/designer/portfolio/products/{id}/och-{id}.json` |
| Selected card manifest | `output/pdf/och-business-cards-v5/och-card-manifest-v5.json` |
| Card back builder | `output/pdf/och-business-cards-v5/och-build-cards-v5.py` |
| Labelled card front builder | `output/pdf/och-business-cards-v3/och-build-type-studies.py:32` |
| Card primitives | `output/pdf/och-business-cards-v1/och-build-cards.py:33` |
| Logo-only front builder | `context/domains/external/work/career/designer/portfolio/brand/och-build-charcoal-assets.py:30` |
| Brand authority | `context/domains/external/work/ventures/ocharo/och-brand-guidelines.md` |
| Current asset map | `context/domains/external/work/career/designer/portfolio/brand/och-brand-assets.md` |

## Catalogue template

### Design and page map

Actual PDF page size: **594.96 × 841.92 pt** (A4 working format). No AcroForm exists; editability must come from the application document/layout, not PDF form filling.

| Pages | Template | Existing content |
|---|---|---|
| 1 | Full image cover | Generated Abaya photograph, white wordmark, collection/city footer |
| 2 | Category opening, one product | Abaya 01 |
| 3 | Category opening, four products | Baska 01–04 |
| 4 | Category continuation, two products | Baska 05–06 |
| 5 | Category opening, two products | Korset 01–02 |
| 6 | Category opening, four products | Kostyum 01–04 |
| 7 | Category opening, four products | Nimcha 01–04 |
| 8 | Category continuation, six products | Nimcha 05–10 |
| 9 | Category continuation, six products | Nimcha 11–16 |
| 10 | Three-photo collage | `look-v05` |
| 11 | Four-photo collage | `look-v03` |
| 12 | Four-photo collage | `look-v04` |
| 13 | Three-photo collage | `look-v06` |
| 14 | Designer/craft capsules | Name, two supplied text blocks, original portrait, mannequin scene |
| 15 | Full image overview | Sixteen mannequin looks, collection title and statement |
| 16 | Full image contacts | Warm room, logo, Instagram/Telegram/phone links and location |

Paper `#F6F0E5`; ink `#302B25`; muted `#847562`; rules `#D1C3AF`; capsule `#E9E0D3`; price block `#FBF8F2` with `#CDBDA8` border. Interior margins are 28 pt. Category headings are Georgia 27 pt; body is Arial, with 8 pt metadata and a joined two-column price panel.

The local font files have misleading historical names: `och-didot.ttf` and `och-times.ttf` both identify themselves as **Georgia**, not Didot/Times; `och-photo.ttf` is Georgia Bold; `och-body.ttf` is Arial; `och-bold.ttf` is Arial Bold. These names were inspected from font metadata. Match actual family metrics when reproducing the template.

### Product editing

- Keep stable product IDs and category identity separate from edition order.
- Initial family order is `abaya`, `baska`, `korset`, `kostyum`, `nimcha`.
- Import the exact manifest order; allow reordering without renaming product IDs.
- Each product imports its current first `assets.model_images` entry. Five category openings use the first family's three-quarter transparent cutout.
- Edit single-unit and 10+-unit prices independently as integer UZS amounts.
- Preserve **1 piece** and **10+ pieces** thresholds; prices for **2–9 remain unspecified**.
- Sizes are exactly `S, M, L, XL, XXXL`; do not normalize to XXL.
- Use source metadata for material/decoration and localized labels. Latest v22 correction is `Shoyi` / `Шёлк` / `Silk` on Baska 03, Korset 02 and Kostyum 03.
- Treat imported product values as an edition snapshot; show changed values as a draft and preserve source hash/revision.
- Prefer selected-product settings and reorder controls, with a page preview reflecting the actual export.
- Product crop controls should preserve frame geometry and edit image `positionX`, `positionY`, and `zoom`.

### Photo collage geometry

Coordinates are percentages measured from the page's top-left. Frame layout and image crop are separate fields.

| Page | Photo | Frame x/y/w/h | Focal x/y | Zoom |
|---|---|---|---|---|
| 10 | `img_7696` | `0/0/100/39` | `.5/.45` | `1` |
| 10 | `img_7708` | `0/39/50/61` | `.5/.35` | `1.025` |
| 10 | `img_7705` | `50/39/50/61` | `.5/.4` | `1.025` |
| 11 | `img_7632` | `0/0/58/100` | `.5/.5` | `1` |
| 11 | `img_7631` | `58/0/42/33⅓` | `.5/.27` | `1` |
| 11 | `img_7627` | `58/33⅓/42/33⅓` | `.5/.2` | `1` |
| 11 | `img_7635` | `58/66⅔/42/33⅓` | `.5/.3` | `1` |
| 12 | `img_7647` | `42/0/58/100` | `.5/.5` | `1` |
| 12 | `img_7646` | `0/0/42/33⅓` | `.5/.3` | `1` |
| 12 | `img_7648` | `0/33⅓/42/33⅓` | `.5/.22` | `1` |
| 12 | `img_7650` | `0/66⅔/42/33⅓` | `.5/.25` | `1` |
| 13 | `img_7752` | `38/0/62/100` | `.5/.5` | `1` |
| 13 | `img_7750` | `0/0/38/50` | `.5/.45` | `1` |
| 13 | `img_7751` | `0/50/38/50` | `.5/.45` | `1` |

Do not import the removed `look-v01`/`look-v02` spreads into the default edition. The v22 builder retains their historical layout definitions but filters them out. Use `media/cleaned/` crop derivatives selected through `car-journal-v19-photo-crops.json`, not whichever similarly named original appears first. `photo_placements` alone omits focal positions; the extracted audit manifest includes them from the builder.

Recommended controls: select a frame on preview; replace image from the imported media palette; horizontal/vertical crop, scale, frame position/size; reorder frames and pages; reset selected frame to source; undo/redo; toggle template margins/guides. Preserve source images and limit adjustments to document geometry.

## Business card template

Working size **85 × 55 mm**; actual PDF size **240.9449 × 155.9055 pt**. Current files are RGB trim proofs without production bleed/ICC configuration. Existing printing is confirmed, but unknown press settings must not become invented app defaults.

### Front

- Background: `brand/backgrounds/och-silhouettes-landscape-soft-blur-v1.png`, 1536 × 1024 RGB; source already carries the selected soft blur.
- Fill frame using centered cover crop; apply black `#000000` overlay at **0.52** opacity.
- Existing white wordmark: width **151 pt**, horizontal center, top **48 pt**, alpha-cropped aspect ratio.
- Labelled v5: descriptor centered at top-baseline **115 pt**, Arial **8.3 pt**, white.
- Logo-only v6: same composition, descriptor hidden; no QR.

### Back

- Background `#F6F0E5`; primary text `#302B25`; muted text `#6C5D4B`; divider `#DED0BC`.
- Name: x=17 pt, top-baseline=33 pt; Georgia 14 pt.
- Role: x=17 pt, baseline=49 pt; Arial 8.3 pt, muted.
- Divider: x=17 to width−17, y=63 pt; 0.5 pt.
- Contacts: icon x=17 pt, text x=32 pt; baselines **84, 101, 118 pt**, Arial 8.3 pt.
- City: x=17 pt, baseline=height−15; Arial 8 pt, muted.
- Preserve exactly three contact destinations, with link fields distinct from visible labels.

Current contacts: `ocharostile` → `https://www.instagram.com/ocharostile/`; `Ocharo_Style` → `https://t.me/Ocharo_Style`; `+998 97 733 25 15` → `tel:+998977332515`.

Expose front/back switch, locale, text fields, optional front descriptor, font size, background image/color, overlay color/opacity, logo choice/position/scale, object order and guides. Provide 85×55 trim PDF, per-face PNG/SVG and editable document JSON. Preserve the original v5 copy when importing that exact template. V22 subsequently confirms English `Maftuna Togayeva`, while historical card v5 still uses `Maftuna Tog‘ayeva`; make that source difference visible instead of silently claiming the older card was updated.

## Logo and brand asset presets

| Asset | Dimensions | Role |
|---|---|---|
| `och-logo-approved-ivory-v1.png` | 1536×1024 RGB | Selected master/reference, background included |
| `och-logo-black-transparent-v1.png` | 1536×1024 RGBA | Light-background composition |
| `och-logo-white-transparent-v1.png` | 1536×1024 RGBA | Dark-background composition |
| `och-studio-six-nimchas-square-r06.png` | 1254×1254 RGB | Sharp square background source |
| `och-charcoal-logo-profile-v8-1080.png` | 1080×1080 RGBA | Selected social-profile output/reference |

White logo alpha bounds: `[141,293,1392,782]`; black logo alpha bounds: `[69,276,1501,759]`. They are different raster derivatives, not perfect inversions. Crop via a viewBox/container using the actual alpha bounds; do not squeeze the full 3:2 canvas into the wordmark slot.

Selected profile v8 uses solid white lettering, original 52% black tint and an 8% larger wordmark than v7. Its tighter circular presentation intentionally clips outer lower skirt edges but retains the complete wordmark. Treat circular preview as a display mask; export a square asset. Keep frosted studies as historical optional variants, not default identity.

The asset notes identify small contour differences between transparent exports and the selected master. Existing accepted compositions use the white derivative. Preserve it as imported artwork; a new exact vector master is a distinct design task.

## Application document and import recommendations

Use one typed editor document envelope with `schemaVersion`, `kind`, `templateId`, `sourceEdition`, `locale`, stable document ID, title and optimistic revision. Separate shared `assets[]` metadata from page/frame/text/price/layout nodes. Catalogue and card templates share image placement and export primitives while retaining their own typed settings.

Asset records should contain an application asset ID, source relative path, SHA-256, media kind, dimensions, provenance class and optional source-product ID. Store only selected source payloads; keep historical provenance arrays in source records, outside routine page state. Preserve local originals; generate bounded browser derivatives in the new product's own storage/import area when implementing. Do not bundle 89.5 MB of full source images into the initial JavaScript module.

For a useful initial editor, prioritise: persisted named documents; live accurate preview; catalogue prices/order/photos/crops; two-sided business-card composition; import/export document JSON; downloadable rendered output. Keep generated/styled content clearly identified in source metadata, including cover/model scenes and mannequin background.

The existing builders are references, not dependencies to execute blindly: they assume macOS font paths, traverse canonical source folders, and write multiple historical artifact paths. Port template geometry into app-owned rendering logic. A future server exporter should take a validated immutable document snapshot plus assets, never arbitrary filesystem paths or executable HTML.

## Verification performed for this audit

- Rendered all 16 v22 English PDF pages; visually inspected the complete contact sheet.
- Rendered and inspected both Uzbek v5 card faces and the current v6 logo-only front.
- Verified PDF page sizes and absence of form fields.
- Read the complete current builder geometry, copy, pricing schema, card rules and provenance notes.
- Inspected font metadata, selected asset dimensions/alpha bounds and SHA-256 hashes.
- No source images, source documents, Ocharo Studio files or application code were changed.

Visual scratch: `/private/tmp/ocharo-template-review/`. Source PDF references for parent answer: `car-ocharo-journal-v22-en.pdf` and `och-charcoal-card-uz-trim-v5.pdf` above. Follow PDF source-citation format if surfaced to the user.

## Authorized implementation follow-up (2026-09-27)

Implemented only inside the migrated Brand Workspace repository at `workbench/brand-platform-ws/workbench/brand-workspace`. Original Ocharo assets, historical builders and Ocharo Studio remain unchanged.

- `engineering/scripts/import-ocharo-templates.py` reproducibly imports 16 catalogue pages × 3 locales, 29 garments, and 2 card faces × 3 locales. It reads sources without executing historical builders.
- `engineering/scripts/README.ocharo-templates.md` documents geometry, derivative retention, provenance, font fallbacks and export limitations.
- `Templates.ts` provides isolated document factories; JSON seeds use normalized millimetre geometry.
- `public/templates/ocharo/manifest.json` records 74 current curated assets and original/derivative hashes. Earlier served derivatives remain available for immutable saved-document compatibility.
- `DesignCanvas.vue` renders the document with image crops, opacity, rounded frames, rotation and text styling. Drag/resize emits one immutable completed transaction; cancelled movement and locked elements are covered by tests.
- `CatalogueTools.vue` edits one-piece/10+-piece prices and moves garment groups between slots on a page. `CatalogueLayout.ts` also renumbers explicit folios after page reordering while preserving model identifiers.
- `CardTools.vue` edits contacts, switches faces, controls descriptor/photo visibility and offers Original/Ivory/Charcoal/Olive/Clay themes and six logo options. Four color variants preserve the exact original alpha silhouette and are labeled editor variants in the manifest.

Verification: complete frontend typecheck passed; 12 focused template/canvas tests passed; lane ESLint passed. Root owns the persistence/export/runtime verification. Preview follows the existing HTML geometry; the historical printed PDF differs slightly in text baseline and gradient implementation. No claim of pixel-identical or press-ready output is made.

### Browser-export visual verification

Rendered root-created `.local/verification/design-review/catalogue-review.pdf` and `business-card-review.pdf` with Poppler. Inspected all 16 catalogue page compositions and a separate 29-panel price contact sheet: all 58 displayed prices are legible within their quantity panels. The export includes the root's saved garment reorder and price edit. Four collage spreads retain the intended frame compositions; the designer, collection overview and contact pages render without missing assets. Card front exports the new ivory background and charcoal recolored wordmark correctly, demonstrating the embedded alpha SVG is exportable.

The first business-card PDF exposed clipped edited Location text because the original frame retained the short source word's width. Fixed with a reusable `FitTextFrame.ts` helper and integrated it into CardTools; root wired generic card text editing too. The helper expands ordinary edited text to available safe width, retains center/right anchors and reserves multiline height. Friendly contact labels are now shown. `setCataloguePrice` similarly allows longer prices within their quantity columns. Focused regression coverage now totals 15 tests across template, text-frame and gesture checks. Updated card PDF verification remains with root's final browser pass.

Final card re-export verified: the full edited `Toshkent · O‘zbekiston` line is visible in the rendered PDF contact face and exported PNG; the name, role, divider, social handles and telephone remain intact. Evidence: `/private/tmp/ocharo-template-review/final-card-2.png` and `.local/verification/design-review/business-card.png`. No further template-renderer fixes were needed.

Rendered evidence: `/private/tmp/ocharo-template-review/export-contact.jpg`, `/private/tmp/ocharo-template-review/all-prices.jpg`, and `export-card-1.png` / `export-card-2.png`.

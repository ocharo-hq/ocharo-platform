# Catalogue builder verification

> Preserved pre-consolidation evidence; not an active plan. Current work belongs to the canonical version track and backlog. Historical permissions and runtime observations are not current authorization.

*Last updated: 2026-09-29*

## Delivered workflow

Catalogue designer → **Build from products** selects a canonical brand and either its saved
Commerce draft or an immutable published release. Search selects garments; each garment offers its
attached photographs. Product and continuation layouts accept one or two images; collage accepts
one to six. Selection order inside the image picker determines image placement.

- **Product page:** hero/detail images, localized fabric/decoration/description, sizes and price tiers.
- **Product continuation:** larger detail photographs and garment copy.
- **Photo collage:** one to six photographs in a balanced grid.
- **Cover:** a new edition receives an editable A4 cover; every element remains editable.
- **Price continuation:** more than four canonical tiers automatically creates extra pricing pages,
  twelve tiers per page. Quantity labels remain attached to their edition price controls.
- **Edition management:** insert after the selected page, rename, reorder, duplicate, remove and undo.
- **Persistence:** immutable design revisions, reopen, optimistic conflict retention and Save copy.
- **Export:** whole-edition PDF, selected-page PNG and editable JSON round trip.

Every generated page records source brand, saved draft revision, product revision, layout,
currency and selected image hashes. The copied edition remains independent: editing its price does
not change the canonical product. Source revisions are descriptive provenance, never authorization.
All source media still uses the existing organization-protected Commerce image route.
Unreviewed media is visibly marked while selecting. An edition does not grant publication approval.

The existing source catalogue remains available through **Edit current template**. Its original
sixteen pages and original assets were preserved.

## Verification executed

- Pinned Node 24.11.0: all **110 frontend tests** pass, including eleven new composition/selection checks.
- Production frontend build, strict typecheck and lint pass.
- Real trusted-HTTPS Desk session at `https://localhost:8340`, using the isolated synthetic review organization.
- Selected a canonical product/image; created product, continuation and collage pages; reordered pages;
  edited a quantity price; saved and reopened the complete four-page edition.
- Real API snapshot retained source IDs/revisions, page order and the edited price.
- A deliberate competing API save caused 409 in the visible editor; the unsaved title remained intact.
  Save copy persisted the retained edition under a new identity.
- Desktop 1440px and mobile 390px: no horizontal page overflow or JavaScript errors.
- Browser PDF download: 656,119 bytes; Poppler confirms four A4 pages, no embedded JavaScript.
  All four pages rendered and were visually inspected for images, clipping, folios and price placement.
- Browser PNG download: selected collage page rendered successfully.
- Existing private Media authorization and immutable design API required no backend schema or API change.

Ignored evidence stays in `.local/catalogue-review/`: `verify.mjs`, `result.json`,
`builder-desktop.png`, `builder-mobile.png`, `edition-desktop.png`, `edition-mobile.png`,
`catalogue.pdf`, `collage.png` and the four PDF page renders. Credentials remain only in the workspace's
existing private review files; no credential is embedded in the verifier or evidence.

## Boundaries

The designer manages page/product ordering. Customer sales orders, checkout, payments and fulfillment
are not part of this increment. The current public Catalogue remains the Marketing-owned projection
of an approved canonical release. Exporting a private edition does not publish a buyer page or social post.

PDF pages are rasterized at the existing catalogue export resolution to preserve image crops and
script rendering; they are printable A4 pages, not tagged/searchable PDF typography. Canonical photos
must remain available to the authenticated organization when an editable design is reopened or exported.
No automatic upstream refresh overwrites edition edits; insert a new page from a chosen newer revision.

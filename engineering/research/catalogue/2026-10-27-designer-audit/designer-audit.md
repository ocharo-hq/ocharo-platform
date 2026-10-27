# Catalogue designer audit

*Last updated: 2026-10-27*

Do the Catalogue pilot's Web designer and PDF designer work? This audit drove both designers, the gallery and
the public reader in Chromium against a real `OcharoCatalogue.Api`, and left the
[browser suite](../../../codebase/ocharo-catalogue.frontend-services/apps/web/e2e/e2e.md) behind
(`pnpm e2e` from the Catalogue frontend workspace).

## Verdicts

| Designer | Verdict |
|---|---|
| Web designer | **Works end to end.** Pages, order, theme, copy, image group and price flags save and release exactly as designed; the draft preview matches the released reader product for product. Two small bugs were fixed (phone layout, Persian quantity digits). |
| PDF designer | **Works end to end, with usability and output-format gaps.** Exports have the exact page count, order and sizes; current-page PDF/PNG match the canvas; RTL text renders and is right-aligned; undo restores randomized images; releases serve the same pages. The PDFs are images without a text layer, and the canvas has no fit-to-width zoom. |

Both designers are fit for the owner's review; nothing found blocks it. Owner-review checkboxes in
[v0.1](../../../planning/version-track/v0.1/v0.1.md) stay open: this is automated evidence, not owner review.

## Scenarios

Run on 2026-10-27: **24 of 24 browser tests pass** in 1.7 min (2 workers), after the fixes below. Every test
fails on an uncaught page error or unexpected console error; the only console error in the run is the
deliberate `409` of the stale-save test. No request failed. All data is synthetic.

| Scenario | Result | Evidence |
|---|---|---|
| Gallery: create (name → slug, outputs, language) | Pass | `gallery.spec.ts`; revision 1, inactive by default |
| Gallery: rename name and slug | Pass | Revision 2; an existing catalogue keeps its slug until edited |
| Gallery: duplicate | Pass | "(copy)" title, inactive, unique `-copy-` slug |
| Gallery: activate / deactivate | Pass | Revision 3, `isActive` saved |
| Gallery: archive (Remove) → history remains | Pass | Hidden from gallery, listed as archived, revisions 1–3 readable; [screenshot](evidence/gallery-desktop.jpg) |
| Web-only, PDF-only, combined → matching designer | Pass | Only the matching designer buttons and cross-links; PDF outputs start with a cover page |
| PDF designer of a web-only catalogue | Fail → **fixed** | Gave no notice; see F4 |
| Web: ordered pages (add, rename, reorder, product order, inactive page) | Pass | Saved snapshot equals the expected pages; [screenshot](evidence/web-designer-desktop.jpg) |
| Web: theme with undo and redo | Pass | `theme-grid` ↔ `theme-editorial` |
| Web: shared copy (header, collection, cover note, footer) | Pass | Saved in `copyByLocale.en`, shown in preview and reader |
| Web: image group (detail) | Pass | Each product shows only its detail image, by media hash |
| Web: price flags (wholesale hidden) | Pass | One price line per product |
| Web: preview matches the released reader at `/c/:slug` | Pass (English); Persian fail → **fixed** | Sections, order, names, descriptions, details, image hashes, price lines, theme, cover note and footer equal; [preview](evidence/web-designer-desktop.jpg), [reader](evidence/web-reader-desktop.jpg); see F2 |
| Web: preview at 1440 / 768 / 390 | Pass | No overflow inside the preview; [390](evidence/web-preview-390.jpg) |
| PDF: exact pages (add continuation page, set 148 × 210 mm, reorder, save) | Pass | Saved sizes `[210×297, 210×297, 148×210, 210×297]` |
| PDF: whole-catalogue export | Pass | 4 pages, sizes and order as designed, PDF title and creator set, one image per page; OCR reads the product name and `420,000`; [sample PDF](evidence/exported-whole-catalogue.pdf), [page 2](evidence/exported-page-2-a4.jpg) |
| PDF: text in the exported PDF | Open | pdf.js extracts no text: every page is a JPEG image; see O1 |
| PDF: current-page PDF | Pass | 1 page, 148 × 210 mm, file `…-pages-3.pdf` |
| PDF: current-page PNG | Pass | 1748 × 2480 px (300 dpi); mean difference to the PDF page 1.3/255, to the designer canvas 0.8/255; [PNG](evidence/exported-page-3-a5.jpg), [canvas](evidence/designer-canvas-page-3.jpg) |
| PDF: randomize catalogue images → undo | Pass | Undo restores every image on every page, redo reapplies, page-level randomize leaves other pages alone |
| PDF: refresh product fields | Pass | Renamed product text updates; every element keeps its frame |
| Uzbek, Russian, English interface over English content | Pass ×3 | Translated headings and buttons, `html[lang]` follows the interface, content keeps `lang="en"`, no untranslated interface phrase from the message table; [Russian](evidence/pdf-designer-ru-interface.jpg), [Uzbek](evidence/pdf-designer-uz-interface.jpg) |
| Switch interface language (account menu) / catalogue language | Pass | Each leaves the other unchanged; the saved catalogue language stays until saved |
| Arabic, Persian, Urdu: web RTL layout | Pass ×3 | `dir="rtl"`, product names start at the right edge, no overflow at 390 px; [ar](evidence/web-preview-ar-390.jpg), [fa](evidence/web-preview-fa-390.jpg), [ur](evidence/web-preview-ur-390.jpg) |
| Arabic, Persian, Urdu: glyphs in the exported PDF | Pass ×3 | Name drawn and right-aligned in its frame; OCR reads Arabic-script text back; [ar](evidence/exported-page-ar.jpg), [fa](evidence/exported-page-fa.jpg), [ur](evidence/exported-page-ur.jpg), [ar PDF](evidence/exported-page-ar.pdf) |
| English catalogue switched to Persian → PDF text turns RTL | Pass | Canvas text `direction: rtl`, `lang="fa"` |
| Missing translation: web omits the product | Pass | Designer marks it "Needs review"; the reader shows only the translated product; a page holding only that product is omitted; [reader](evidence/reader-web-ru.jpg) |
| Missing translation: PDF omits its bound page | Pass | Designer lists the excluded page; export and release have 3 of 4 pages; [released PDF](evidence/released-catalogue.pdf) |
| Release: prepare and publish from the designer | Pass | 2.5 s for 3 PDF pages |
| Release: reader web and PDF views, page images | Pass | 3 PNG page images, A4 ratio, > 1000 px wide; [phone](evidence/reader-pdf-390.jpg), [page](evidence/released-page-2.jpg) |
| Release: whole and page PDF downloads | Pass; names fail → **fixed** | 3 pages / 1 page, A4; see F1 |
| Release: revoking a translation after publication | Pass | The published release keeps serving it until re-prepared or withdrawn (immutable) |
| Release: republish, earlier release link | Pass | New edition replaces the old; `?release=<first>` still serves the first |
| Page duplicate, remove, undo; revision restore; recovery copy; stale save | Pass | Stale save refused with "This item changed elsewhere", edits kept |
| Phone, tablet and desktop widths | Pass after fix | No page overflow at 390 / 820 / 1440; web layout picker clipped at 390 → **fixed** (F3); PDF canvas needs panning (O2); [web 390](evidence/web-designer-phone.jpg), [PDF 390](evidence/pdf-designer-phone.jpg), [PDF 1440](evidence/pdf-designer-desktop.jpg) |
| Export speed with photo-sized images | Pass | 7 pages, twelve 3000 × 4000 images: export 7.6 s, prepare 6.7 s (budget 60 s / 120 s) |
| "Use original design" | Blocked | Its template images are Git LFS objects that this container could not fetch |
| Running Desk's designer | Blocked | See [E3 comparison](#e3-desk-and-pilot-designers) |

## Bugs

### Fixed

Each fix is its own commit on this branch, with a browser test that failed before it.

| # | Severity | Bug and reproduction | Fix |
|---|---|---|---|
| F1 | Medium | Released downloads arrive as `catalogue.pdf`, `page-<uuid>.pdf` and `page-<uuid>.png`. Publish a catalogue with PDF output → open `/c/<slug>` → **Download catalogue PDF**. The API's `Content-Disposition` overrode the reader's `download` names. | `fix: named released catalogue downloads as the reader offers them` — the API names files `ocharo-<slug>-<locale>.pdf` and `ocharo-<slug>-<page>.pdf/.png`, matching the reader. |
| F2 | Low | The Web designer preview prints quantity bands in Latin digits where the reader localizes them: Persian preview `۴۲۰٬۰۰۰ UZS · 1–9 عدد`, reader `… · ۱–۹ عدد`. Create a Persian catalogue with a priced product, compare the preview with `/c/<slug>?locale=fa`. | `fix: matched preview quantity digits to the released reader` |
| F3 | Low | At 390 px the Web designer's page-template picker runs off the screen; "Photo collage" is cut off. Open a web designer on a phone. | `fix: kept the web layout picker within phone widths` — icons only below 640 px; labels stay as accessible names and tooltips. |
| F4 | Low | The PDF designer of a web-only catalogue gives no notice; pages can be designed and export then fails with "PDF output is disabled for this catalogue." Open `/catalogues/<web-only id>/pdf`. | `fix: showed the disabled-output notice in the PDF designer`, mirroring the Web designer's notice in all three languages. |

### Open

Larger or design-level problems, left for the owner while designer work is frozen pending E3.

| # | Severity | Problem and reproduction |
|---|---|---|
| O1 | Medium | Exported and released PDFs are one JPEG per page: no text layer, so text cannot be searched, selected or read by a screen reader, and only OCR recovers it. Export any PDF and try to select text. (Known since the 2026-10-01 review; it keeps scripts and crops exact.) |
| O2 | Medium | The PDF canvas zoom is a fixed width (540–1080 px) with no "fit". At 1440 × 900 the 100 % A4 sheet (720 px) is wider than the ~600 px canvas column and scrolls sideways; on phones and tablets the page needs two-way panning ([screenshot](evidence/pdf-designer-phone.jpg)). Desk offers **Fit**. |
| O3 | Low | Sliders and colour pickers record an undo step per input event: 12 arrow-key steps on **Image zoom** need 12 undos, and a mouse drag records dozens. The history holds 50 steps, so one drag can push out an earlier randomize. `CataloguePdfDesignerPage.vue` range and colour inputs use `@input`; `UseCatalogueDocument.ts` caps the stack at 50. |
| O4 | Low | Resizing a PDF page keeps millimetre positions, so A4 content is cropped on A5. The export check warns "Extends beyond the page", but nothing rescales. Set a product page to 148 × 210 mm and export ([page](evidence/exported-page-3-a5.jpg)). |
| O5 | Low | A newly added catalogue language (e.g. Arabic) starts with empty shared labels. PDF export and preparation then stop with "Add the ar quantity unit in shared catalogue text", which appears only when exporting. |
| O6 | Low | The export check warns "Text extends beyond its frame" for Arabic and Persian product details that render without clipping (Urdu does not warn), adding a confirmation to every such export. Probably the tall bounds of the Arabic fallback font; cause not verified. |
| O7 | Low | Element labels are stored English data (`Catalogue title`, `name`, `details`, `price`, `Product image 1`) and appear in the inspector, the selection tag and export-check lines under the Uzbek and Russian interface ([screenshot](evidence/pdf-designer-ru-interface.jpg)). |
| O8 | Low | Shared-copy text areas set no `lang` or `dir`, so Arabic, Persian and Urdu labels are typed in left-to-right fields. Desk sets both. |
| O9 | Low | PDF price labels write quantity bands in Latin digits next to Persian-digit prices on Persian pages (`۴۲۰٬۰۰۰` above `1–9 عدد`). F2 aligned the web preview with the reader; the PDF label was left unchanged. |
| O10 | Low | The retained "Catalogue saved." notice follows the operator into the public reader and covers its Refresh button ([screenshot](evidence/web-reader-desktop.jpg)). |
| O11 | Question | Images with the reference role ("Namuna") are eligible for web and PDF output; Desk's contract admits only non-reference media. Confirm which is intended. |
| O12 | Note | Urdu renders in Naskh, not Nastaliq. No Arabic-script font is bundled, so PDF glyphs come from the operator's system fonts; this audit ran with Noto fonts installed. |

## E3: Desk and pilot designers

Desk's designers were compared from code, not run. PostgreSQL 16 starts here, but Desk's `global.json` pins
.NET SDK 10.0.300 while only 10.0.113 could be installed (the .NET download host is blocked in this container),
and its catalogue routes require a signed-in organization member. Desk sources:
`brand-workspace.frontend-services/apps/web/src/presentation/workspace/` (`CatalogueWebDesigner.vue`,
`DesignEditorPage.vue`, `CatalogueSettingsPanel.vue`, `CatalogueBuilder.vue`, `CataloguePublication.vue`).

| Capability | Desk | Pilot |
|---|---|---|
| Accounts, roles, tenancy | Organization membership, edit/approve roles, brand scope | None; public when deployed |
| Storage and releases | PostgreSQL, Commerce drafts, immutable brand releases consumed by Marketing | SQLite, own releases, own reader at `/c/:slug` |
| Web preview | Publishable view only; "preview all active pages" toggle | Draft view with "Needs review" badges and empty slots; one page at a time |
| Web page editing | Add, duplicate after current, move selected, template select, activate | Same, plus per-row move buttons, product order within a page, icon template picker |
| Shared copy fields | `lang` and `dir` on text areas | No `lang`/`dir` (O8) |
| Image selection | Per-image publication approval, credits | Active flag only; reference images eligible (O11) |
| PDF page creation | Template catalogue (16 pages), catalogue builder (products, layouts, images per page), connect an existing layout to products | Cover, product, continuation and collage pages from one product; original design (uz/ru/en) |
| PDF editing | Elements, crop and zoom, typefaces, page size, keyboard nudging, product-field binding with price tiers, photo replace, randomize page or catalogue | Same set |
| Canvas zoom | 65–150 % with **Fit** | Fixed widths, no fit (O2) |
| Export | Whole or selected-page PDF, page PNG, PDF preview toggle, editable design file export/import, export-check review that jumps to elements | Whole and current-page PDF, page PNG; export check as a confirmation list |
| History | Revision list with page previews; restore as unsaved copy; recovery copy | Revision list without previews; restore as unsaved draft; recovery copy |
| Publication | Prepare/publish with a reviewed-pages confirmation and publication note | Prepare, publish, withdraw; releases list |
| Business cards | Same editor | Not present |
| Verification | Unit and API tests with PostgreSQL containers | Unit tests and this 24-test browser suite |

For the decision:

- Desk's PDF tooling is broader (builder, fit zoom, PDF preview, editable files, review step) and it owns
  accounts and approvals, which the pilot needs before it stores anything valuable.
- The pilot's verified strengths worth keeping either way: the draft web preview with review badges, product
  order within a web page, and the right-to-left handling proven here.
- Whichever stays canonical, O1 (raster-only PDFs) applies to both: Desk's `DesignExport.ts` also writes one
  JPEG per page.

## What stays unverified

- Real photographs: file sizes and the 64 MiB whole-PDF and 256 MiB release limits. Synthetic images compress to
  0.3 MiB for 7 pages; real photos at 240 dpi will be far larger.
- "Use original design" and the brand logos (Git LFS objects unavailable here; the logo shows as a broken image in
  reader screenshots).
- Desk's designers in a browser.
- Firefox, Safari and real touch devices; only Chromium ran, with emulated viewports.
- Deployed dev and prod hosts (public mode, ingress); everything ran on localhost.
- Product-editor scenarios (prices, approvals, review questions); the suite seeds products through the API.
- Typography of right-to-left and other added languages; the checks prove direction and glyph presence, not quality.

## Environment and baseline

| Item | Value |
|---|---|
| Backend tests | 89 passed (`OcharoCatalogue.Tests`), plus 21 `Ocharo.Sso.Tests`; the same counts after the fixes |
| Frontend unit tests | 30 test files (33 files in `tests/` including 3 helpers), 218 tests passed; unchanged after the fixes |
| Typecheck | Passed (39 SFCs); now also covers the browser suite through `tsconfig.e2e.json` |
| Lint | 0 errors, 2 existing warnings in `tests/ProductReviewUi.test.ts` |
| Toolchain | .NET SDK 10.0.113, Node 24.22.1, pnpm 10.33.2, Playwright 1.63.0 with Chromium |
| Transport | HTTPS on both apps, unchanged; Vite used an Ubuntu `mkcert` binary because the plugin's own download was refused |
| Optional tools | `tesseract` with `eng`, `ara`, `fas`, `urd` for OCR checks; Noto fonts |

The suite starts its own API (`18278`) and Vite (`18282`) with a temporary store, so it never touches a
developer's `App_Data`.

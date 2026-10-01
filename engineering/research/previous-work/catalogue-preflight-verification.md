# Catalogue export preflight

> Preserved pre-consolidation evidence; not an active plan. Current work belongs to the canonical version track and backlog. Historical permissions and runtime observations are not current authorization.

*Last updated: 2026-09-29*

## Delivered workflow

The authoring editor checks the exact requested export pages before raster PDF or PNG downloads.
The buyer-edition preparation action checks the complete immutable saved design before rendering
or uploading its bundle. Buyer downloads continue serving their previously approved immutable bytes.

- **Check pages** inspects the entire open design without saving, exporting or publishing it.
- Current-page PDF and PNG inspect only that page; selected-page PDF inspects only the chosen pages.
- Missing or unloadable visible images block the operation and identify the page and element.
- Text-frame overflow and rotated frames crossing the page edge require explicit review.
- Intentional crops and background bleed remain permitted after acknowledgement.
- Fully transparent elements are skipped, matching the PDF renderer.
- **Show element** returns to the matching page, selects that element, and closes the check.
- Downloads from unsaved changes require acknowledgement and preserve those edits in the editor.
- Every operation rechecks its own snapshot. Acknowledgement is never reused after another operation or edit.
- Navigating away cancels pending review; failed checks cannot claim a download or upload succeeded.
- Publication still requires a saved design revision plus the existing owner's separate approval and note.

## Rendering and provenance

`DesignTextLayout.ts` is shared by the renderer and preflight. It retains the existing font selection,
letter spacing, line wrapping, alignment, baselines and clipping geometry. Checks measure the actual
font glyph extents at the export DPI, rather than approximating text length from character counts.

The current catalogue source assets, geometry, product records and saved editions remain unchanged.
Preflight does not resize frames, rewrite content, modify product prices or republish existing editions.

The original three templates each contain 16 pages. Actual Chromium checks found:

| Locale | Checked pages | Warnings | Missing media errors | Existing warning detail |
|---|---:|---:|---:|---|
| Uzbek | 16 | 1 | 0 | Abaya heading on page 2 |
| Russian | 16 | 5 | 0 | Headings on pages 2, 6, 7, 8, 9 |
| English | 16 | 4 | 0 | Headings on pages 2, 7, 8, 9 |

These warnings are small **left glyph overhangs**, not missing lines or large blocks of clipped text:
Abaya/Абая is approximately 0.191 mm; English Vest is 0.177 mm; Russian Жилет/Жакет-кейп is 0.112 mm.
No right, top or bottom overflow was measured for these specific baseline warnings. The original
appearance remains available after explicit warning review.

For each locale, page 2 was rendered at 240 DPI and compared against the previously verified PNG.
All three comparisons used 1983 × 2806 pixels and found **zero differing RGBA channels**.

## Verification

- Frontend suite: **135 tests passed** across 24 files.
- ESLint and strict TypeScript/SFC checks passed.
- Production frontend build passed; existing large-chunk advisory remains.
- Browser checks passed against the local authenticated review editor:
  - original 16-page inspection and page-specific heading warnings;
  - acknowledgement required before current-page PDF download;
  - issue navigation selects the correct page and element;
  - a new check runs before buyer-edition preparation;
  - cancelled edition preparation performs zero edition upload/publication requests;
  - unsaved healthy current-page PNG exports despite another page's missing media;
  - whole-edition PDF blocks that missing media with no continue action;
  - desktop and mobile dialog layout;
  - original saved design restored for review and session signed out.
- Native dialog centering was verified against the production stylesheet at 1440 × 1100 and 390 × 844.

New regression cases cover rotated frames, multiline clipping, unbreakable words, selected-page
scope, unavailable and absent media, request reuse, hidden decorations, source immutability,
intentional bleed, cancellation, fresh warning acknowledgement, and publication cancellation.

Local evidence is ignored under `.local/catalogue-preflight-review/`:
`baseline.json`, `browser-result.json`, `dialog-centered-desktop.png`,
`dialog-centered-mobile.png`, `missing-image-block.png`, and reviewed export artifacts.
The original edition publication records remain under `.local/catalogue-edition-review/`.

## Limits

Preflight detects geometric clipping and source-image availability. It does not establish spelling,
translation quality, source accuracy, image publication rights, print color accuracy or printer bleed
requirements. Those remain part of the owner's existing content review. This pass does not change
raster PDF output into editable text, CMYK artwork or a printer-specific production format.

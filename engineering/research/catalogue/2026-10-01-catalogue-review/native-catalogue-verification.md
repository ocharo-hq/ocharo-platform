# Native Catalogue verification

*Last updated: 2026-10-01*

## Scope

- Target: Catalogue frontend `8282`, `OcharoCatalogue.Api` HTTPS `8278`.
- Existing Desk features remain preserved. Catalogue now owns its native authoring screens.
- Existing Catalogue SQLite products and revisions are retained; new tables use additive initialization.
- No live database write, service restart, or content publication was performed by this implementation.

## Local runtime finding

The frontend's HTTPS proxy correctly targets `8278`. The Rider API process inspected during diagnosis
was started before the HTTPS-only configuration changed and still served HTTP on `8279`.
Rebuilding its binary did not replace that running process. Stop that debugging session and launch
`OcharoCatalogue.Api: https`; confirm `https://localhost:8278` in its startup output.

## Verification

| Check | Result | Evidence and boundary |
|---|---|---|
| Native backend | 51/51 passed | [Log](native-evidence/backend-tests.log); test hosts/temp databases, not live Rider data |
| Native frontend | 65/65 passed, 13 test files | [Log](native-evidence/frontend-tests.log) |
| TypeScript and Vue templates | Passed, 26 SFCs compiled | [Build log](native-evidence/frontend-build.log) |
| Production build | Passed | Existing large-chunk advisory remains; no deployment performed |
| ESLint | Passed | [Log](native-evidence/frontend-lint.log) |
| Native browser workflows | Passed with intercepted synthetic API fixtures | [Receipt](native-evidence/results.json); product save, independent approval, gallery, Web save, PDF product selection |
| Product editor mobile | Exact 390×844 viewport at x=0, y=0 | [Screenshot](native-evidence/product-dialog-mobile.png) |
| Public reader | Six Web/PDF fixtures at 390/820/1440 passed | [Receipt](native-evidence/public-responsive.json); no horizontal overflow; actions at least 44px |
| Browser PDF downloads | Two-page whole catalogue and one-page product export; both A4 | [Inspection](native-evidence/pdf-inspection.json); actual jsPDF downloads |

Poppler rendered the downloaded pages. The cover and product text/prices fit without clipping.
The product fixture uses intentionally blank synthetic image pixels; this proves placement/rendering,
not garment-photo quality. PDF pages are rasterized; text is not searchable/selectable. Additional
language typography still requires owner review, especially RTL and font coverage.

The final mobile dialog positioning CSS was additionally verified in the browser after the build;
it does not alter the compiled application interfaces. No live Catalogue product data was edited.

### Screens and review paths

- `/`: [Products](native-evidence/products-desktop.png), [product modal](native-evidence/product-dialog-desktop.png).
- `/catalogues`: [Catalogue gallery](native-evidence/catalogue-gallery.png).
- `/catalogues/:id/web`: [Web designer](native-evidence/web-designer.png).
- `/catalogues/:id/pdf`: [PDF designer](native-evidence/pdf-designer.png).
- `/c/:slug`: [Public web phone](native-evidence/public-web-phone.png), [public PDF phone](native-evidence/public-pdf-phone.png).

Screens are synthetic verification artifacts, not approved final brand designs or live publication.

### Committed implementation

- `c22b4fd`: native product, design and release backend.
- `1e74f12`: native UI, product review, designers, public reader and regression tests.

Earlier Desk or Marketing evidence is historical and must not be counted as native Catalogue verification.

## Remaining integration

Catalogue is still a loopback-only local editor. Account-aware navigation, remote write authorization,
and reconciliation with authenticated Desk Commerce remain separate from this local Catalogue build.
The public reader route reads explicitly published releases, not working product drafts.

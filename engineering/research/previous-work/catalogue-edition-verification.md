# Exact catalogue editions

> Preserved pre-consolidation evidence; not an active plan. Current work belongs to the canonical version track and backlog. Historical permissions and runtime observations are not current authorization.

*Last updated: 2026-09-29*

## Original content

The existing `ocharo-journal-v22` source remains unchanged: sixteen pages, twenty-nine garment
variants, Uzbek/Russian/English copy, original quantity prices, original images and page geometry.
Its imported page dimensions are `209.888666… × 297.010666… mm`, displayed as
`209.889 × 297.011 mm`. The operator editor exposes this original through its existing template
and saved original-language designs; the generic product composer is an additional workflow.

The historical printed PDFs use a different PDF/font rendering path. The imported editable design
preserves the source composition and content, but is not a byte-identical historical PDF.
The new fixed preview and downloadable editions use the same application renderer.

## Fixed preview and exports

- **PDF preview:** each page uses the same 240-DPI canvas renderer as catalogue publication.
  Responsive fitting and zoom scale the whole fixed-proportion page; page text never reflows.
- **Whole edition PDF:** all pages in their saved order and original physical dimensions.
- **Current page PDF:** a standalone one-page PDF.
- **Selected pages PDF:** selected pages in edition order; their printed folios remain unchanged.
- **Page PNG:** the existing high-resolution standalone page export.
- **Editable JSON:** the existing complete editable snapshot, preserving source geometry.

Page selection rejects empty, duplicate and stale IDs. Failed image loads visibly fail the preview
or export; the preview discards late results from a previously selected page. Export caches repeated
images only within that render operation.

## Buyer edition contract

A saved, clean catalogue design can prepare an immutable buyer edition for an explicitly selected
Platform brand. A single 240-DPI canvas per page produces its PNG, its standalone PDF, and its image
inside the full catalogue PDF. The upload carries the saved design ID/revision and the expected
Commerce revision. The backend derives title, locale, page identities and geometry from that saved
source, rather than trusting client-supplied page metadata.

Preparation stores private, unapproved artifacts. An owner must explicitly confirm the exact pages,
images, contacts and all quantity prices and provide a note before publishing. A design edit does not
silently change a published edition. The existing canonical release and organization authority remain
in control. Original catalogue tiers are intentional edition content; buyer-product projections retain
their separate private-field filtering.

Multipart file fields are `pdf`, `page-{id}.png` and `page-{id}.pdf`, plus JSON string `metadata`:
`{expectedRevision,sourceDesignId,sourceRevision}`. Preparation returns `CommerceDraft`; explicit
publication returns `CommerceRelease`. The public catalogue feed exposes `edition` with immutable
whole-PDF and per-page image/PDF/PNG URLs.

## Executed verification

- 121 frontend tests pass, including selection, preview-race/error, multipart and explicit-review checks.
- Production build, strict typecheck and lint pass.
- Existing original-language PDFs were rendered read-only: sixteen pages each, `594.96 × 841.92 pt`.
- Authenticated original-page preview loads at the isolated review runtime.
- All three original saved designs completed preparation and explicit publication in the isolated local review runtime.
- All 99 anonymously downloaded artifacts match the exact uploaded bytes: three whole PDFs, 48 page PDFs, 48 PNGs.
- Preview page two and its published PNG have identical pixels in all three languages (`1983 × 2806`, 240 DPI).
- All three whole PDFs contain sixteen pages; all 48 page PDFs contain exactly one page.
  Every checked PDF retains `594.96 × 841.92 pt` (the imported original page dimensions).
- Current-page export contains page two; selected-page export contains pages two and eight in that order.
  Poppler renders match the corresponding whole-PDF pages pixel for pixel.
- Standalone high-resolution page PNG remains `2479 × 3508` (300 DPI).
- All 48 newly rendered pages were visually inspected through three contact sheets. English pages two and eight
  were inspected at 1600 pixels for image crops, quantity labels, price readability and clipping.
  The original/new page-two pair preserves composition and content; the documented font-baseline differences remain.
- Desktop and 390-pixel mobile preview workflows pass; mobile has no document-width overflow.
  Browser errors were empty. The review operator logged out after verification.

These are local review publications, not a production launch. No deployed environment was changed.

## Review entry and receipts

Open `https://localhost:8340/organizations/454138d5-f5a6-4452-8e4b-39cf3155d71c/catalogue`,
then **Saved designs → Ocharo · original catalogue · EN**. The other two saved originals are
labelled UZ and RU. The editor currently uses this saved-design selector rather than a document deep link.

| Locale | Saved design | Local edition | Local release |
| --- | --- | --- | --- |
| UZ | `74039bfe-5c80-48e4-9d89-016507f06760` | `53d8b70a-73f3-4651-9e24-68c0fcb59278` | `63c8e9db-0fc8-447f-8eac-9761f763ff25` |
| RU | `c02d7dfc-e84f-47eb-bffa-6a60e1dc82ce` | `ce698b1e-4ad8-4f94-ae3a-9835ba7439b4` | `bd9653d0-faea-4b47-a837-2f9f7f9789cb` |
| EN | `a1994839-fb48-423e-a80a-271a1967434f` | `88b1d32f-9d6b-403a-a1a8-589ee4907ce0` | `6f4b5679-f723-4443-b377-d9715f98f4e9` |

The `.local/catalogue-edition-review/` evidence directory contains:

- `result.json`: per-artifact SHA-256/byte receipts and immutable edition/release IDs.
- `pdf-verification.json`: PDF geometry, page count, selected-page and preview equality results.
- `{uz,ru,en}-catalogue.pdf`: the actual publicly served full editions downloaded during verification.
- `{uz,ru,en}-final-contact-sheet.jpg`: all sixteen rendered PDF pages per language.
- `en-original-new-page2.jpg`: historical original and new export at equal scale.
- `final/en-detail-08.png`: the six-piece pricing page at readable resolution.
- `en-current-page2.pdf`, `en-selected-pages2-8.pdf`, `en-current-page2.png`: actual operator downloads.
- `en-preview-desktop.png`, `en-preview-mobile.png`: authenticated fixed-page preview screenshots.

Private browser receipts and visual evidence live under `.local/catalogue-edition-review/`.
No historical source asset, product JSON, HTML or PDF was edited.

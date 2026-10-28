# Catalogue browser suite

*Last updated: 2026-10-27*

Playwright tests that drive the gallery, the Web designer, the PDF designer and the public reader in Chromium
against a real `OcharoCatalogue.Api`. Findings and results: the
[designer audit](../../../../../research/catalogue/2026-10-27-designer-audit/designer-audit.md).

## Run

From the pnpm workspace root:

```sh
pnpm e2e                      # whole suite
pnpm e2e rtl.spec.ts          # one file
pnpm e2e -g "PDF designer"    # by title
```

Requirements: the .NET 10 SDK, the workspace's Node and pnpm, and Playwright's Chromium
(`pnpm --filter @ocharocatalogue/web exec playwright install chromium` when it is missing).
The optional `tesseract` command with the `eng`, `ara`, `fas` and `urd` languages enables the checks that
read exported PDF text; without it those checks are skipped and noted in the test annotations.

## Isolation

By default the suite starts its own API on `18278` and Vite on `18282`, with a fresh temporary SQLite store and
media root that are removed when the run ends. It never reads `appsettings.Local.json` or a developer's
`App_Data`. Each test seeds its own synthetic products and catalogues through the API, with a run token in every
name and slug, so tests can share one store and run in parallel.

`CATALOGUE_E2E_URL` points the suite at an already running Catalogue instead (for example
`https://localhost:8282`). The tests then write products, catalogues and published releases into that store.
`CATALOGUE_E2E_WEB_PORT`, `CATALOGUE_E2E_API_PORT` and `CATALOGUE_E2E_WORKERS` override the defaults.

## Output

- `test-results/e2e/<test>/`: downloaded PDFs and PNGs, rendered PDF pages, screenshots and `diagnostics.json`
  (console errors, failed requests, error responses, export timings and annotations).
- `playwright-report/`: the HTML report; traces are kept for failed tests.

Every test fails on an uncaught page error or an unexpected console error; a test that expects one, such as a
refused stale save, allows it explicitly.

## Layout

| File | Covers |
|---|---|
| `gallery.spec.ts` | Create, rename, duplicate, activate, archive; web-only, PDF-only and combined catalogues |
| `web-designer.spec.ts` | Ordered pages, theme, copy, image group, price flags; preview against the released reader |
| `pdf-designer.spec.ts` | Exact page sizes and order; whole and current-page PDF/PNG exports; randomize and undo; field refresh |
| `languages.spec.ts` | Uzbek, Russian and English interface over English content; untranslated interface text |
| `rtl.spec.ts` | Arabic, Persian and Urdu content: right-to-left layout, PDF glyphs, Persian price digits |
| `release.spec.ts` | Missing translation omitted from web and PDF; release, reader, downloads; pinned releases |
| `designer-safety.spec.ts` | Page duplicate/remove/undo, revision restore, recovery copy, stale-save conflict |
| `layout.spec.ts` | Gallery and both designers at phone, tablet and desktop widths |
| `export-timing.spec.ts` | Export and preparation time with photo-sized images |
| `support/` | API seeding, synthetic images, PDF inspection, pixel checks and fixtures |

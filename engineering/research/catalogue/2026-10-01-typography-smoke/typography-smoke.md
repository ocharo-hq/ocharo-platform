# Catalogue typography export smoke

Verified **2026-10-01** with Chrome **154.0.8037.58** on macOS. The actual `renderCatalogueBundle` and export preflight produced three fixture pages, a combined PDF, and individual PNGs. All three pages reported zero preflight issues. Each PNG was visually inspected: Arabic glyphs were connected and right-aligned, Chinese text wrapped without added spaces, and mixed Uzbek/Cyrillic text displayed without missing glyphs or clipping.

## Evidence

- `results.json`: browser version, measured lines, overflow flags and preflight report.
- `ar.png`: Arabic heading, RTL paragraphs and Arabic numerals.
- `zh-Hans.png`: Chinese heading and paragraph wrapping at two frame widths.
- `uz.png`: Uzbek apostrophes, Latin text and Cyrillic in the same frames.
- `catalogue-typography.pdf`: combined three-page catalogue export.
- `fixture.ts`: fixed sample content and dimensions; calls the real renderer.
- `run.mjs`: portable fixture build and isolated browser runner.

These are generated test fixtures, not approved catalogue pages. This proves glyph rendering and basic layout on the tested browser/font environment. It does not certify translations, all 25 languages, or font availability on other operating systems. Actual product translations and each final edition still need editorial and PDF review.

## Repeat

Prerequisites: Node 24, the frontend workspace dependencies installed, Google Chrome, and Playwright available either to the frontend's Node resolver or through `OCHARO_PLAYWRIGHT_MODULE`. No dependency or package manifest changes are required by this runner.

From the `ocharo-platform` repository root:

```sh
OCHARO_PLAYWRIGHT_MODULE=/absolute/path/to/playwright/index.mjs \
  node engineering/research/catalogue/2026-10-01-typography-smoke/run.mjs
```

Omit the environment variable if `playwright` already resolves from the frontend package. A separate verification installation may be used, for example `npm install --prefix /tmp/ocharo-browser-verification playwright`, with the module path `/tmp/ocharo-browser-verification/node_modules/playwright/index.mjs`. Installing this optional tool is a separate prerequisite, not an action performed by the runner.

The runner finds application sources relative to its own file and creates a fresh temporary output directory, which it prints. An optional final argument selects a dedicated output directory. The runner overwrites generated files and its `dist/` subdirectory there, so use a new directory. Do not use this checked-in evidence directory for routine reruns.

`OCHARO_BROWSER_CHANNEL` may select another installed Chromium channel; it defaults to `chrome`. The browser uses a temporary profile and fulfills the test origin in memory. Other browser requests are blocked. No Ocharo service is started, stopped or restarted, and no live products, sessions or catalogue documents are accessed.

After rerunning, inspect all three PNGs and the PDF before replacing this evidence. A passing numerical report alone does not establish visual quality.

# Ocharo logo family — shared needle revision

*Last updated: 2026-10-01*

## Result

Every composition uses one authored thin needle: identical outline, eye, taper and proportions. The source is defined once in `build.cjs`, exported to `source/needle.json` and `source/needle.svg`, and embedded unchanged into each composition.

[Open the review gallery](index.html). [View the verification evidence](verification.json).

## What changed

- Replaced independent needle drawings with one reusable SVG path.
- Removed the thicker, rounded micro alternative. The small icon now uses the same complete symbol.
- Reused one oval component for symbol, app tile, avatar and both small-icon treatments.
- Reused one lettering component for ink/reverse wordmarks and the endorsement.
- Rebuilt the wordmark once around the shared needle; it still forms the h stem.
- Exported matching transparent ink and white PNGs.
- Added actual 16, 24 and 32 pixel favicon PNGs without optical reshaping.

The needle is oriented at 42 degrees in compact compositions and 27 degrees in the wordmark to fit the h. Both use uniform scaling. Rotation and placement differ; the path and proportions do not.

## Deliverables

Eight compositions each have an SVG layout and PNG export in `assets/`:

1. `ocharo-wordmark-ink`
2. `ocharo-wordmark-reverse`
3. `ocharo-symbol-ink`
4. `ocharo-tile`
5. `ocharo-micro` — compatibility filename; same symbol geometry
6. `ocharo-micro-reverse`
7. `ocharo-avatar`
8. `ocharo-endorsement`

Six additional favicon PNGs cover ink/reverse at 16, 24 and 32 pixels.

## Verification

`verification.json` records:

- All eight SVGs contain exactly the same needle path and even-odd eye cutout.
- All five compact compositions use the same placement and scale.
- All three wordmark compositions use the same placement and scale; the endorsement translates the complete wordmark.
- Compact compositions embed the same oval pixels; wordmarks embed the same lettering pixels.
- Light/dark PNG pairs have byte-identical alpha masks.
- The small-icon and main-symbol transparent PNGs have byte-identical alpha masks.
- Favicon dimensions are exact and light/dark alpha masks match.

The gallery was rendered at desktop and mobile sizes. All images loaded; neither layout overflowed horizontally.

## Source and limits

The needle is a new authored vector component guided by the thin compact draft. It is consistent across this revision, not a claim of pixel-identical tracing of the earlier generated needle.

The existing oval and lettering were separated with the built-in image generator once. Their PNGs are retained under `source/`, with exact prompts in `prompts.json`. Those contours can differ slightly from the earlier raster drafts.

The SVG files are mixed compositions: a true vector needle plus embedded raster masks for the oval or lettering. They are not wholly vectorized logo masters. The endorsement uses editable Arial/Helvetica text for “by”.

The eye becomes less visible at 16 pixels; it has deliberately not been thickened or enlarged. App and avatar files have square backgrounds, with rounded/circular masks applied only in the gallery preview.

Previous drafts remain intact. No application, published Brand release or social profile was replaced.

## Rebuild

With Node.js available:

```sh
node build.cjs
```

To render PNGs, install Playwright with Chromium in the execution environment, or set `OCHARO_PLAYWRIGHT_MODULE` to its module location:

```sh
node render.cjs
```

To verify, use Python with Pillow installed:

```sh
python3 verify.py
```

The build reads only the checked-in local source components; it does not rerun image generation.


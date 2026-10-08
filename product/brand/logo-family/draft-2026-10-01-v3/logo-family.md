# Ocharo logo family — original master restored

*Last updated: 2026-10-01*

## Result

The original approved wordmark is the source again. Its needle keeps the original length, width, angle, eye, taper and position relative to the lettering. There is no replacement needle, separately scaled stem or regenerated lettering in the wordmark.

[Review the original beside the restored exports](index.html).

This revision supersedes the independently generated first family and the manually drawn needle in revision 2. Earlier files remain preserved for history.

## Source

The byte-identical approved source is saved as `source/approved-master.png`.

SHA256: `32f2efeb381b92fdbf228c6f551909c8d1f78bc1212f1c6b79778cbbf8bcd33e`.

Original location: `mft-10x-ws/context/domains/external/work/career/designer/portfolio/brand/logo/och-logo-approved-ivory-v1.png`.

All eight SVG compositions embed that same original PNG. No image-generation call was used in this restoration.

## How the exports preserve it

- The full wordmark is one original-source mask. There is no separate needle transformation.
- Transparent ink and white exports share identical alpha masks.
- An SVG filter removes the ivory paper and recolors the ink; it does not redraw the lettering.
- The original reference image remains unchanged, including its paper background.
- The gallery shows original, ink and reverse at matching display sizes.
- The endorsement adds “by” outside the complete original wordmark.

## Compact marks

The symbol, tile, avatar and small icons reuse the original O and a crop of the original needle. Their relative source scale and angle remain unchanged. The needle is translated horizontally into the O; the complete composition is scaled together for export.

The source needle joins the c/h lettering around source rows 560–590. A crop boundary separates that junction for the compact design. It is an original-pixel crop, not a claim that an independent original needle master exists.

The crop preserves the upper head and eye and the full lower pointed section. An original-source eye knockout prevents the O from filling the needle eye.

## Deliverables

`assets/` contains eight SVG layouts and eight matching PNGs:

- Wordmark: ink and reverse
- Compact symbol
- App tile
- Small icon: ink and reverse
- Avatar
- “by Ocharo” endorsement

Six additional PNGs provide 16, 24 and 32 pixel favicons in ink and reverse. The legacy `micro` filename remains, but its geometry matches the main symbol.

SVGs embed the original raster artwork. They are reusable compositions, not fully vectorized logo outlines.

## Verification

`verification.json` records:

- Every embedded source image matches the approved source hash.
- The full wordmark has no independently positioned or scaled needle.
- Light/dark wordmark alpha masks match exactly.
- Compact-symbol and small-icon alpha masks match exactly.
- Original dark pixels in the upper needle and lower pointed section remain within the crop.
- Favicon dimensions are exact.
- Desktop and mobile galleries load without horizontal overflow.

The eye remains small at 16 pixels; no artificial thickening was applied.

## Rebuild

With Node.js:

```sh
node build.cjs
```

With Playwright and Chromium available:

```sh
node render.cjs
```

Set `OCHARO_PLAYWRIGHT_MODULE` if Playwright is installed outside local module resolution.

With Python and Pillow:

```sh
python3 verify.py
```

## Publication

These assets are saved for review. No live application, published Brand release, catalogue master or social profile was replaced.


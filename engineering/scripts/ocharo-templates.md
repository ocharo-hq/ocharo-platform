# Ocharo editor template import

Run `python import-ocharo-templates.py --source-root /path/to/mft-10x-ws` from this directory with Pillow, lxml and reportlab available. The importer reads selected original assets and writes only the Brand Workspace application template files. It never executes the historical builders or modifies their sources.

The selected sources are the three catalogue v22 HTML documents, their referenced media, product records, and the business card v5 manifest and historical drawing coordinates. Outputs are 16 catalogue pages in Uzbek, Russian and English; two 85 × 55 mm card faces in each language; and 74 curated image/vector derivatives. Images are bounded to 1800 pixels, with original and derivative hashes retained in `public/templates/ocharo/manifest.json`.

`src/domain/workspace/templates/*.json` contains editable millimetre geometry. Snapshots reference asset URLs; they do not contain image binaries. The catalogue preserves the original garment IDs, multilingual wording, page composition and one-piece/10+-piece prices. Pricing for 2–9 pieces remains unspecified. Photo crops use frame geometry, image fit, crop position and zoom. The original wordmark silhouette is preserved; four additional editor color variants use its alpha mask and are identified as editor variants in the manifest.

The default card is the historical Charcoal veil v5. Its original spelling and contact copy are retained and editable. A logo-only front hides the descriptor without deleting it. Color palettes and solid backgrounds are editor options, not retrospective approval records.

Templates follow the existing HTML geometry and original PDF dimensions. Browser/export text uses Georgia and Arial when available, with system fallbacks. These assets do not bundle licensed system font binaries. Browser font metrics, resized image derivatives and the HTML linear cover fade can differ from the historical print PDF. The editor exports RGB artwork; printer-specific bleed, crop marks, CMYK profiles and physical proofing are outside the template format.

Reimport deliberately resets generated seeds to the selected source edition. It does not change saved user documents. The importer writes deterministic asset names but never deletes earlier derivatives: saved immutable document snapshots may reference any previously served URL. Keep earlier assets for document compatibility even when the current import manifest no longer lists them. Review the manifest when updating source editions.

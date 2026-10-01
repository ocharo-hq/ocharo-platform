# Flows

*Last updated: 2026-09-28*

1. **Import** — Sozlamalar → Import: workspace records become products (active when named + priced + imaged), images stored once by hash.
2. **Correct a product** — open → edit copy/prices/images → Saqlash (revision checked; a stale tab gets a 409 and must reload).
3. **Reprice** — Mahsulotlar → select → ±% with rounding, or Narxlar sheet per cell; prices return to draft until confirmed.
4. **Publish a lookbook** — To‘plamlar → order products → publish → Katalog varag‘i → choose language → print / PDF.
5. **Feed** — Marketing/Blog read `/api/public/catalogue`; only active products and published collections appear.

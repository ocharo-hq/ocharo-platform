# ventures.ocharo-catalogue — Engineering Planning

> Preserved pre-consolidation evidence; not an active plan. Current work belongs to the canonical version track and backlog. Historical permissions and runtime observations are not current authorization.

*Last updated: 2026-09-28*

> Roadmap + tracker. Future ideas: `backlog.md`. Operational rules: `rules.md`.

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| r0.1 — first build | Products, prices, images, revisions, collections, copy grid, print sheet, import/export, public feed | Built + verified locally 2026-09-28; owner review open |
| r0.2 — operations | Fabric registry, reservations against orders, image pipeline hookup (stock per size and the merchant order sheet shipped in r0.1) | Planned |
| r0.3 — hosting | Authentication (SDK identity), shared Ocharo sign-in, deployment | Planned |

## Component tracker

| Component | State | Notes |
|---|---|---|
| Backend | 18 tests green | services, import/export, in-memory HTTP (seed, 409, multipart upload, cross-origin rejection); opt-in real-data import: 35 products / 29 active |
| Frontend | typecheck, lint, 12 tests, build | Uzbek operator UI; browser-checked grid, editor save → v2, price sheet, print sheet |
| Feed consumers | Blog reads it (pieces in stories); Marketing sync | see sibling repos |

## Decisions

- 2026-09-28 — Separate platform repo; siblings consume the public feed.
- 2026-09-28 — Document + snapshot storage (EnsureCreated, no migrations yet); revisit when the model stabilizes.
- 2026-09-28 — Import activates products that have an Uzbek name, a price and an image; copy stays draft.

# ventures.ocharo-brand — Engineering Planning

> Preserved pre-consolidation evidence; not an active plan. Current work belongs to the canonical version track and backlog. Historical permissions and runtime observations are not current authorization.

*Last updated: 2026-09-28*

> Roadmap + tracker. Future ideas: `backlog.md`. Operational rules: `rules.md`.

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| r0.1 — first build | Logo versions per slot, identity, releases with diffs, appearances, public kit + tokens, consumer tracking, Catalogue + Blog adoption | Built + verified locally 2026-09-28; owner review open |
| r0.2 — reach | SVG masters, Marketing caption handles, Studio watermark, guideline PDF | Planned |
| r0.3 — hosting | Authentication (SDK identity), shared Ocharo sign-in, deployment | Planned |

## Component tracker

| Component | State | Notes |
|---|---|---|
| Backend | 5 tests green | seed + release 1, publish flow (stable URL only after publish, stale 409, pinned release, no-change 409), appearances + tokens, embargoed draft/future artwork + boundary-capped caches + event links, boundary/CORS |
| Frontend | typecheck, lint, 4 tests, build | Uzbek operator UI; browser-checked overview |
| Consumers | Catalogue print sheets, Blog site | see sibling repos |

## Decisions

- 2026-09-28 — Separate platform repo; consumers read published releases over HTTP.
- 2026-09-28 — Draft/release split with full snapshots so platforms can pin a release.
- 2026-09-28 — Logo files are content-addressed; the stable slot URL is the only mutable address.

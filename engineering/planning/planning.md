# Engineering planning

*Last updated: 2026-09-26*

## Current track

| Track | Scope | State |
|---|---|---|
| [r0.1](rough-track/r0.1/r0.1.md) | Adapted template foundation | Done; developer requested continuation |
| [r0.2](rough-track/r0.2/r0.2.md) | Authenticated content preparation | Technical work verified; developer review pending |

No version track is active during this coordinated product bootstrap. Later product work remains in the
[backlog](backlog.md); this baseline does not assign speculative release versions.

## Continuation gate

The developer created `04a13f5` and configured the GitHub remote. The native repository switch confirms
automatic commits ON for the current task. These checkpoints permit the authorized scaffold work;
later tasks still inspect the current native state under [operations](../operations/operations.md).

## Decisions

| Decision | Reason |
|---|---|
| One sibling platform repository | Separate product, authorization, persistence, and release boundary from Studio |
| Content & Publishing first | Existing source inputs and an explicitly requested operating workflow |
| Vue frontend | Current shared convention supersedes the template's React starter |
| PostgreSQL and separate worker | Durable execution and shared API/worker persistence are planned product needs |
| Preserve baseline during import | The initial human commit is the foundation of this repository's history |
| Defer active registration until code exists | IDE entries must point to actual solution/frontend paths |

## Backlog

The ordered [backlog](backlog.md) carries preparation, delivery, SDK completion, and later modules.
Detailed current work belongs only in the rough track.

## Log

- 2026-09-26 — Established the documentation baseline and inspected the standard template.
- 2026-09-26 — Verified the adapted foundation, 30 tests, clean publish, and packaged restart recovery.
- 2026-09-26 — Developer requested continued scaffolding; began authenticated preparation under `r0.2`.
- 2026-09-26 — Verified preparation with 79 tests, HTTPS browser/package flows, and session/export persistence.

## Evidence

[Template audit](../research/template-audit.md) and [verification](verification.md) distinguish current
source evidence, adopted changes, and checks not yet executed.

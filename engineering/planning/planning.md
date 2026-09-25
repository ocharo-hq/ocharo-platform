# Engineering planning

*Last updated: 2026-09-26*

## Current track

| Track | Scope | State |
|---|---|---|
| [r0.1](rough-track/r0.1/r0.1.md) | Complete adapted template foundation | Planned; follows the developer's initial commit |

No version track is active while the rough scaffold is planned. Later product work remains in the
[backlog](backlog.md); this baseline does not assign speculative release versions.

## Continuation gate

The user requested the repository baseline first, their initial commit second, and full scaffolding
afterward. Verify that initial commit and inspect the native repository commit status through the
[operations sequence](../operations/operations.md) before application implementation. Commit automation
requires the exact user directive and effective ON status; natural-language intent does not change it.
Scaffolding and staging are authorized work; an OFF flag forbids automatic commits, not those operations.

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

## Evidence

[Template audit](../research/template-audit.md) and [verification](verification.md) distinguish current
source evidence, required adaptations, and checks not yet executed.

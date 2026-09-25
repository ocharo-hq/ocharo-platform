# Brand Workspace

*Last updated: 2026-09-26*

A shared brand-operations platform for preparing, approving, publishing, and evaluating content.
Content & Publishing is the first module. Ocharo is the first organization; garment-specific behavior
belongs in optional apparel modules. **Brand Workspace is the working name.**

## Repository state

This is the documentation baseline for the developer's first commit. No application code, running
service, provider integration, allocated development port, or production deployment exists yet.
The full template-based scaffold follows that commit, as requested.

The target is a .NET 10 backend serving a Vue 3 frontend, PostgreSQL persistence, and an independently
running integration worker. Shared wow-two libraries provide reusable capabilities. The existing
React/SQLite template needs the adaptations recorded in the [template audit](engineering/research/template-audit.md).

## Start here

- [Product definition](product/product.md): users, value, module boundaries, and evidence of usefulness.
- [Current state](product/context.md): completed baseline, continuation gate, and unresolved external inputs.
- [Architecture](engineering/architecture/architecture.md): runtime, data ownership, shared libraries, and Studio boundary.
- [Engineering plan](engineering/planning/planning.md): scaffold sequence and later product milestones.
- [Scaffold track](engineering/planning/rough-track/r0.1/r0.1.md): the next concrete build scope.
- [Template audit](engineering/research/template-audit.md): inspected source, drift, and adoption procedure.

## Canonical analysis

The broader platform analysis remains in [Maftuna's Ocharo platform folder][analysis]. This repository
owns adopted product requirements and engineering execution; it does not duplicate the business corpus.
Ocharo Studio remains an independent creative product with optional export-based integration.

## Git handover

The developer creates the first commit. Automatic commits remain off until the native repository
switch receives the exact user directive documented in [operations](engineering/operations/operations.md).
No remote repository or publication is implied by this local baseline.

[analysis]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform.md

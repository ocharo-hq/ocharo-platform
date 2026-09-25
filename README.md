# Brand Workspace

*Last updated: 2026-09-26*

A shared brand-operations platform for preparing, approving, publishing, and evaluating content.
Content & Publishing is the first module. Ocharo is the first organization; garment-specific behavior
belongs in optional apparel modules. **Brand Workspace is the working name.**

## Repository state

The developer committed the documentation baseline as `04a13f5` and established
[the GitHub repository](https://github.com/sulton-max/ventures.brand-workspace).
The adapted scaffold is implemented and technically verified under [r0.1](engineering/planning/rough-track/r0.1/r0.1.md).

The foundation uses a .NET 10 API serving a Vue 3 frontend, PostgreSQL persistence, and an
independent worker. Shared wow-two libraries provide reusable capabilities. Template adaptations
are recorded in the [audit](engineering/research/template-audit.md); executed results live in
[verification](engineering/planning/verification.md).

This foundation does not yet implement accounts, brands, drafts, approvals, scheduling, or provider
connections. Ocharo remains the first planned organization. No social account is connected.

## Start here

- [Product definition](product/product.md): users, value, module boundaries, and evidence of usefulness.
- [Current state](product/context.md): current milestone and unresolved external inputs.
- [Architecture](engineering/architecture/architecture.md): runtime, data ownership, shared libraries, and Studio boundary.
- [Engineering plan](engineering/planning/planning.md): scaffold sequence and later product milestones.
- [Scaffold track](engineering/planning/rough-track/r0.1/r0.1.md): the concrete foundation scope.
- [Template audit](engineering/research/template-audit.md): inspected source, drift, and adoption procedure.

## Canonical analysis

The broader platform analysis remains in [Maftuna's Ocharo platform folder][analysis]. This repository
owns adopted product requirements and engineering execution; it does not duplicate the business corpus.
Ocharo Studio remains an independent creative product with optional export-based integration.

## Git and runtime operations

The developer's first commit is preserved. Ordinary commits follow the native repository switch;
the human publishes. See [operations](engineering/operations/operations.md) for repository state and
[deployment](engineering/deployment/deployment.md) for local commands.

[analysis]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform.md

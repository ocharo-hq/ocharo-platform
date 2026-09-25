# Brand Workspace — current state

*Last updated: 2026-09-26*

## Current milestone

The developer committed the documentation baseline as `04a13f5` and created the GitHub remote
`https://github.com/sulton-max/ventures.brand-workspace.git`. The adapted .NET/Vue/PostgreSQL/worker
foundation is implemented and technically verified under [r0.1](../engineering/planning/rough-track/r0.1/r0.1.md).

`ventures.brand-workspace` is the repository name, `BrandWorkspace` the .NET namespace, and
`brand-workspace` the code slug. The commercial name remains a working name. No provider account,
organization membership, or content workflow is implemented by this foundation.

## Confirmed direction

- A sibling venture repository owns the general platform; Studio keeps its creative scope.
- Content & Publishing is the first platform module; Ocharo is the first organization.
- Reusable capabilities belong in wow-two libraries; apparel semantics stay optional.
- The developer created the first commit before full template scaffolding began.
- Native commit permission is checked for each task; documentation never grants it.

## Applied implementation defaults

Current shared conventions require .NET 10 and Vue 3 with strict TypeScript, Vite, Tailwind v4,
and `@wow-two-beta/ui-vue`. The inspected template's React starter is not an exception.
Use a modular backend serving the frontend and a separate worker process. PostgreSQL is the target
shared API/worker store. Executed verification and its limits are recorded separately below.

## Continuation

Review scaffold acceptance under [r0.1](../engineering/planning/rough-track/r0.1/r0.1.md).
The first product milestone afterward is authenticated organization membership and tenant-aware content
preparation with immutable approval and honest manual export. Automated scheduling and one authorized
provider path follow separately. A technical queue probe does not complete that delivery milestone.

The initial Git history and remote remain intact. Check the effective native commit permission under
[operations](../engineering/operations/operations.md) when continuing in another task.

## External inputs

The first provider account, format, approval roles, and authorized publication example still need
concrete input before a live integration probe. They do not block framework or preparation work.
No tasks are assigned to Maftuna by this document.

Evidence and executed checks: [verification](../engineering/planning/verification.md).
Broader opportunities: [canonical platform hub][analysis].

[analysis]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform.md

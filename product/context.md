# Brand Workspace — current state

*Last updated: 2026-09-26*

## Current milestone

Repository definition baseline, prepared for the developer's initial commit. The working repository
name is `ventures.brand-workspace`; its .NET namespace is `BrandWorkspace` and code slug is
`brand-workspace`. The user has not selected a final commercial name.

No application code or runtime has been scaffolded. No provider account is connected. No development
ports, remote Git repository, deployment, or marketing publication have been created.

## Confirmed direction

- A sibling venture repository owns the general platform; Studio keeps its creative scope.
- Content & Publishing is the first platform module; Ocharo is the first organization.
- Reusable capabilities belong in wow-two libraries; apparel semantics stay optional.
- The user creates the first commit before full template scaffolding begins.
- Automatic commits require the native repository directive; this baseline grants no permission.

## Applied implementation defaults

Current shared conventions require .NET 10 and Vue 3 with strict TypeScript, Vite, Tailwind v4,
and `@wow-two-beta/ui-vue`. The inspected template's React starter is not an exception.
Use a modular backend serving the frontend and a separate worker process. PostgreSQL is the target
shared API/worker store. These are design choices; no runtime verification has occurred here.

## Continuation

After the developer commits the baseline, verify the commit, working tree, and native commit status
under the [operations sequence](../engineering/operations/operations.md) before beginning
[r0.1](../engineering/planning/rough-track/r0.1/r0.1.md). Automatic commits require the exact user
directive and effective ON status; checking or enabling that status never means editing its record.
Preserve the baseline and `.git` during the
template import; the existing scaffold script refuses an already existing destination.

The first product milestone after the scaffold is tenant-aware content preparation and honest manual
export. Automated scheduling and one authorized provider path follow separately.

## External inputs

The first provider account, format, approval roles, and authorized publication example still need
concrete input before a live integration probe. They do not block framework or preparation work.
No tasks are assigned to Maftuna by this document.

Evidence and executed checks: [verification](../engineering/planning/verification.md).
Broader opportunities: [canonical platform hub][analysis].

[analysis]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform.md

# Ocharo Platform — current state

*Last updated: 2026-09-29 03:38 PM +05*

## Current Platform build — 2026-09-29

The canonical checkout is `ocharo-ws/workbench/ocharo-platform`, owned by Ocharo.
Ocharo Desk now provides authenticated products, brand identity, collections, media provenance review,
immutable catalogue releases and withdrawal through the existing organization membership authority.
Published buyer responses exclude private notes, source documents, exact stock, wholesale tiers,
unapproved copy and unapproved media. Studio remains independently owned.

Connected apps opens the built-in local Studio, Marketing and Editorial applications with five-minute,
audience-bound access. Live session and organization membership determine delegated authority.
Creative manifests preserve Studio look revisions, asset release hashes, materials and derivative identity.
Imported renders remain unreviewed until an owner approves their publication evidence.

[Platform build verification](../engineering/research/previous-work/platform-build-verification.md) records current
contracts, executed checks and boundaries. Public deployment, provider account connection and real
outbound campaigns remain separate actions. Preserved companion databases and existing review
runtimes were not migrated or overwritten by these changes.

## Canonical catalogue builder — 2026-09-29

Catalogue designer now composes saved canonical products and selected images into product pages,
continuation pages and photo collages. Editions preserve source revision metadata and support
reordering, price overrides, save/reopen/conflicts, whole-catalogue PDF and page PNG export.
[Catalogue verification](../engineering/research/previous-work/catalogue-builder-verification.md) records the real
browser workflow, reviewed exports and the distinction between edition ordering and customer orders.

## Earlier consolidation handoff — historical

The following handoff describes the pre-consolidation snapshot; its pause and placement statements
were superseded by the authorized Ocharo Platform build above.

## Consolidation pause — 2026-09-29

The user requested a handoff for Ocharo consolidation and instructed this chat to stop editing afterward.
This generic product remains in its current checkout; the Ocharo relocation map explicitly leaves its
organizational ownership and relocation undecided. Preserve the existing staged r0.3 implementation.

Confirmed software direction from this chat:

- Brand Workspace's Brand module is the intended canonical brand-identity owner. Reconcile the newer
  standalone Ocharo Brand implementation rather than creating a second independently writable authority.
- Studio remains an independently owned creative product/repository.
- Apps share assets/storage; Brand and Marketing may reuse Catalogue photographs.
- Each app owns its document types, editing flow and business rules. No standalone Documents/file-explorer app.
- Common asset selection, canvas, revision and export mechanisms support those contextual workflows.

The proposed placement is catalogue editions in Catalogue and business cards in Brand. The earlier
two-product-repository recommendation was not accepted as a final migration plan. The newer Ocharo
portfolio includes four separate public applications and Motion; reconcile that broader scope during
consolidation rather than treating this chat's older recommendation as the final repository count.

The interrupted request “pull the choice images here” remains unfulfilled. Three overview alternatives
and card variants are saved in the [review guide](../engineering/research/previous-work/rough-track/r0.3/r0.3-review.md).
The request did not specify which image choices; no preferred layout or brand variant was selected.
No agent-authored code is known to exist only in chat. External editor buffers and unsaved browser edits
were not inspected or recovered; saving remains explicit in the current editor.

The [consolidation handoff](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/ocharo-hq/ocharo-ws/docs/handoffs/brand-workspace-01a0def2-2f06-74a3-9ae1-989e1bde51e8.md)
records current Git/runtime state, preserved work, verification evidence, open decisions and resumption steps.
Application builds and suites were not rerun for this handoff. The milestones below retain their recorded dates.

## Current milestone

The product now lives in Maftuna’s [Brand Platform workspace](../../../README.md).
The current [r0.3 design increment](../engineering/research/previous-work/rough-track/r0.3/r0.3.md) provides
catalogue/card editors, immutable server revisions, PDF/PNG/editable-JSON exports and three overview layouts.
All 80 frontend and 72 backend tests passed, with trusted-HTTPS browser and visual PDF checks.
[Developer review](../engineering/research/previous-work/rough-track/r0.3/r0.3-review.md) remains open; the original
catalogue/card source files and Ocharo Studio stay unchanged.

## Earlier preparation milestone

The developer committed the documentation baseline as `04a13f5` and created the GitHub remote
`https://github.com/sulton-max/ventures.brand-workspace.git`. The adapted .NET/Vue/PostgreSQL/worker
foundation was committed as `d4b1add` under [r0.1](../engineering/research/previous-work/rough-track/r0.1/r0.1.md).
The developer requested continued scaffolding. Authenticated preparation is implemented under
[r0.2](../engineering/research/previous-work/rough-track/r0.2/r0.2.md). Technical verification passed 79 tests,
the HTTPS browser/package workflows, and session/export persistence across container replacement.
Developer review remains open; [verification](../engineering/research/previous-work/verification.md) records the limits.

`ventures.brand-workspace` is the repository name, `BrandWorkspace` the .NET namespace, and
`brand-workspace` the code slug. The commercial name remains a working name. Organizations and brands
are created by operators; no Ocharo identity or real account is seeded by the application.

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

Review the current [editor increment](../engineering/research/previous-work/rough-track/r0.3/r0.3-review.md) and the usable preparation increment under [r0.2](../engineering/research/previous-work/rough-track/r0.2/r0.2.md).
Local email/password operators are provisioned through the administrator CLI; public registration and
email delivery are absent. The workflow provides isolated organizations, brands, immutable PNG/JPEG
uploads, versioned drafts, review, owner approval/revocation, and downloadable manual ZIP snapshots.
Operator setup and HTTPS commands live in [operator access](../engineering/deployment/operator-access.md).

Automated scheduling and one explicitly authorized provider path follow separately. A technical queue
probe or prepared ZIP does not establish that a provider published anything.

The initial Git history and remote remain intact. Check the effective native commit permission under
[operations](../engineering/operations/operations.md) when continuing in another task.

## External inputs

The first provider account, format, approval roles, and authorized publication example still need
concrete input before a live integration probe. They do not block framework or preparation work.
No tasks are assigned to Maftuna by this document.

Evidence and executed checks: [verification](../engineering/research/previous-work/verification.md).
Broader opportunities: [canonical platform hub][analysis].

[analysis]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform.md

## Version ownership

The active plan is `engineering/planning/version-track/v0.1/v0.1.md`; future work is in `engineering/planning/backlog.md`. Existing implementation and historical local checks do not constitute a product release or manual acceptance. Preserved pre-consolidation notes are evidence only; current authenticated contracts and source take precedence.

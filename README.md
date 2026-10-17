# Ocharo Platform — Ocharo Desk

*Last updated: 2026-09-29*

**Ocharo Desk** is the authenticated operator workspace for brand identity, products, media review and
content preparation. Platform owns canonical Brand/Product facts and immutable releases. Marketing
consumes the released buyer projections and owns editorial publishing, distribution and enquiries.
Studio remains an independent creative product with protected Platform access and file-based handoff.

## Dedicated Catalogue app — current review target

The requested authoring app is **Ocharo Catalogue**, frontend `https://localhost:8282`,
backend `OcharoCatalogue.Api` at `https://localhost:8278` with the `https` launch profile.
Its source lives under `engineering/codebase/ocharo-catalogue.{frontend,backend}-services/`.
Products, prices, translations, collections, named catalogues and both designers are edited there.
Desk at `8264` remains a separate operator workspace. A Desk link is not a Catalogue implementation.

Catalogue retains its existing SQLite products, media hashes and product revision history.
New design/release tables are added idempotently; this work does not adopt or overwrite live data.
No automatic synchronization with Desk Commerce exists. Do not edit the same brand in both stores
and imply they are synchronized. Authentication and canonical-store consolidation remain explicit
integration work before deploying Catalogue's loopback-only write APIs publicly.

Routes: `/` products; `/catalogues` saved gallery; `/catalogues/{id}/web` responsive designer;
`/catalogues/{id}/pdf` page designer; `/c/{slug}?locale=en` published reader.
Interface preferences are independent of authored catalogue languages.

## Repository and current build

The canonical repository is [ocharo-hq/ocharo-platform](https://github.com/ocharo-hq/ocharo-platform),
inside the [Ocharo workspace](../../README.md). The GitHub transfer is complete; existing history,
record identities and private runtime data are preserved. The former mft checkout path remains a
compatibility link for the existing process. [Repository setup](../../docs/repository-setup.md) records
canonical identities and retained local origin aliases.

The current build provides:

- Authenticated organizations, live membership checks, owner approvals and revision conflicts.
- Canonical brand/product drafts, immutable releases, locale-specific buyer projections and withdrawal.
- Private media previews, explicit publication provenance and unreviewed Studio derivative intake.
- Five-minute audience-bound connections to Studio, Marketing and Editorial with revocation checks.
- Content preparation, original image uploads, revisioned drafts, review and manual ZIP packages.
- Editable catalogue/card templates and business overview layouts backed by workspace records.

The foundation uses a .NET 10 API serving Vue 3, PostgreSQL persistence and an independent worker.
Published WoW2 SDK packages provide shared capabilities. Preserved Brand/Catalogue companion apps
remain separate source inputs; they do not replace the authenticated canonical Commerce module.

Read the [local product review guide](../../docs/review-guide.md) for all running review entrypoints,
operator access, synthetic fixtures and repeatable startup. The [workspace build record](../../docs/platform-build.md)
and [Platform verification](engineering/research/previous-work/platform-build-verification.md) record executed tests,
browser checks, integration contracts and remaining delivery inputs. Local verification does not mean
production deployment, approved business content or an outbound campaign.

## Prerequisites and local operation

Use .NET 10, the Node version declared by each frontend's `.nvmrc`, pnpm and PostgreSQL according to
[deployment](engineering/deployment/deployment.md). The workspace review guide describes isolated
Docker storage and trusted local HTTPS without replacing the earlier Desk runtime.

**Git LFS is required for the supplied PNG template assets.** Without Git LFS, a checkout contains LFS
pointer text instead of usable PNG images. Set up Git LFS before cloning; after cloning or enabling it
in an existing checkout, run `git lfs pull` to obtain the assets.

Provisioned operator access is documented in [operator access](engineering/deployment/operator-access.md).
Git operations follow persistent repository flags and the active native hook. The
[workspace delivery record](../../docs/platform-build.md#repository-evidence) records the enabled flags
and this chat's cross-workspace hook boundary; use the appropriate WoW2-rooted context for reviewed
commits. Setup documents do not grant Git permission or change remote configuration.

## Start here

- [Product definition](product/product.md): users, value, module boundaries and evidence of usefulness.
- [Current state](product/context.md): implemented product and remaining external inputs.
- [Architecture](engineering/architecture/architecture.md): runtime, data ownership and independent Studio boundary.
- [Engineering plan](engineering/research/previous-work/planning.md): delivery tracks and verification links.
- [Current review](../../docs/review-guide.md): Desk and connected application workflows.
- [Current verification](engineering/research/previous-work/platform-build-verification.md): releases, access, creative intake and browser evidence.
- [Template audit](engineering/research/template-audit.md): inspected source, drift and adoption procedure.

## Foundation and template history

The original documentation baseline `04a13f5` and Brand Workspace history remain intact.
The adapted foundation was completed under [r0.1](engineering/research/previous-work/rough-track/r0.1/r0.1.md),
authenticated preparation under [r0.2](engineering/research/previous-work/rough-track/r0.2/r0.2.md), and catalogue/card
editing and the overview under [r0.3](engineering/research/previous-work/rough-track/r0.3/r0.3.md).
The [verification index](engineering/research/previous-work/verification.md) retains the milestone evidence.

The v22 catalogue is an editable 16-page template in Uzbek, Russian and English. Prices, garment/page
order, photographs, crop and frame geometry can be edited, saved, reopened and exported. The card
editor preserves the wordmark, supports contact editing and color/background variants, and exports
both 85×55mm faces. Three overview layouts use workspace counts. Historical variants, screenshots and
export limitations remain in the [r0.3 review guide](engineering/research/previous-work/rough-track/r0.3/r0.3-review.md)
and [r0.3 verification](engineering/research/previous-work/rough-track/r0.3/r0.3-verification.md).

## Brand and Catalogue source adoption

Brand identity/releases and Catalogue product/price/stock/print companion implementations live here
with their service identifiers, independent SQLite data and operator frontends preserved. They remain
loopback pilots. Desk's authenticated PostgreSQL Commerce module now owns canonical drafts, reviewed
releases and public buyer projections; Marketing implements the separate buyer Catalogue experience.

Preserved donor adoption is explicit and owner-only. `Commerce__AdoptionOrganizationId` must match
the intended receiving organization before any donor file is read; missing or invalid configuration
fails closed. Adoption retains source identifiers, revisions and hashes and does not approve images
for publication. The isolated review does not enable real donor adoption.

- [Adoption guide](engineering/research/adoption/adoption.md): source mapping, commands and historical boundaries.
- [Brand product](product/brand/product.md) and [Brand development](engineering/development/brand/development.md).
- [Catalogue product](product/catalogue/product.md) and [Catalogue development](engineering/development/catalogue/development.md).
- [Adoption verification](engineering/research/previous-work/adoption-verification.md): checks at the consolidated source paths.
- [Current Commerce contracts](engineering/research/previous-work/platform-build-verification.md#contracts): authenticated canonical integration.

## Canonical analysis

The broader business analysis remains in [Maftuna's Ocharo platform folder][analysis]. This repository
owns adopted product requirements and engineering execution; it does not duplicate the private business
corpus. Studio retains its own designs and optional review-package integration.

[analysis]: /Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform.md

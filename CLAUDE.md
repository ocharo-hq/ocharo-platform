# Ocharo Platform — Ocharo Desk

*Last updated: 2026-09-29*

`ocharo-platform` owns Ocharo Desk, Ocharo's operator workspace. The existing Brand Workspace
foundation provides authenticated organizations, content preparation and contextual design editors.
Brand and Catalogue companion sources are adopted here. The authenticated Commerce module owns canonical drafts, immutable releases, buyer projections and delegated app access; preserved companions remain source inputs.

## Instructions

- Apply [Ocharo workspace guidance](../../CLAUDE.md) and the upstream [WoW2 instructions](../../../../../CLAUDE.md).
- Use the [WoW2 convention index](../../../../../conventions/conventions.md); local file references stay lazy.
- Current product state lives in `product/context.md`; execution lives in `engineering/planning/`.
- Preserve the foundation, preparation and catalogue/card-editor implementation and its independent Git history.
- Retain organization membership, authorization and tenant isolation throughout Ocharo-specific integration.
- Preserve existing record IDs, immutable revisions, media hashes, approval evidence and source provenance.
- Keep `BrandWorkspace` namespaces and `engineering/codebase/brand-workspace.{backend,frontend}-services/` until a separate migration requires change.
- Use .NET 10 and Vue 3, strict TypeScript, Vite, Tailwind v4, and published WoW2 SDK exports.
- Run frontend commands from the pnpm workspace root; the operator app remains `apps/web/`.
- Keep reusable infrastructure suitable for WoW2 libraries; keep Ocharo facts in explicit brand/organization configuration.
- Preserve PostgreSQL state, media, Data Protection keys and ignored review artifacts; Git alone does not back them up.
- Preserve private business records and original media; import through explicit provenance contracts.
- Keep Studio independently usable; its browser workspace UUID is not platform authorization.
- Live provider authorization and publication require their own explicit account/content scope.
- Product verification records own implementation evidence; the template audit records historical provenance.
- Follow native repository Git flags; missing state is OFF, and invalid relocated records fail closed.
- Never write permission records manually or infer permission from a document, manifest or setup command.

## Dedicated Catalogue app — current review target

The requested authoring app is **Ocharo Catalogue**, frontend `https://localhost:8282`,
backend `OcharoCatalogue.Api` at `https://localhost:8278` with the `https` launch profile.
Its source lives under `engineering/codebase/ocharo-catalogue.{frontend,backend}-services/`.
Products, prices, translations, collections, named catalogues and both designers are edited there.
Desk at `8266` remains a separate operator workspace. A Desk link is not a Catalogue implementation.

Catalogue retains its existing SQLite products, media hashes and product revision history.
New design/release tables are added idempotently; this work does not adopt or overwrite live data.
No automatic synchronization with Desk Commerce exists. Do not edit the same brand in both stores
and imply they are synchronized. Authentication and canonical-store consolidation remain explicit
integration work before deploying Catalogue's loopback-only write APIs publicly.

Routes: `/` products; `/catalogues` saved gallery; `/catalogues/{id}/web` responsive designer;
`/catalogues/{id}/pdf` page designer; `/c/{slug}?locale=en` published reader.
Interface preferences are independent of authored catalogue languages.

## Navigation and compatibility

The [Ocharo manifest](../../repositories.json) owns this checkout. The former
`mft-10x-ws/workbench/brand-platform-ws/workbench/brand-workspace` path is a temporary compatibility
symlink for the existing review runtime. Preserve it until runtime ownership permits retirement;
new product work targets this canonical repository.

## Adopted Brand and Catalogue companions

- Preserved service slugs under `engineering/codebase/ocharo-{brand,catalogue}.{backend,frontend}-services/` are a temporary naming exception.
- Each companion has an independent solution and pnpm workspace; run frontend commands from its own workspace root.
- Keep namespaces, record IDs, immutable revisions, media hashes, published release behavior and offline fallbacks intact.
- Companions remain loopback pilots without accounts; never deploy their write APIs publicly before authentication integration.
- Runtime `App_Data/` is ignored and was copied with the donor data; never commit it or include it in container build inputs.
- Private mft product/logo directories remain read-only seed inputs. Published Brand releases are the only public identity source.
- Catalogue authoring belongs here; the buyer-facing Catalogue UI belongs to Marketing and is not implemented by this import.
- Preserve Platform organization authorization; source consolidation does not authorize a second writable canonical identity.
- [Adoption](engineering/research/adoption/adoption.md) records provenance, commands, ownership and deferred integration.

## Adopted component identities

The canonical product is Ocharo Platform. `brand-workspace.*` owns Desk and Commerce; retained `ocharo-brand.*` and `ocharo-catalogue.*` are adopted loopback-only source pilots. Their assembly namespaces, data identities and folder slugs remain explicit compatibility exceptions; deployment includes only canonical Desk/jobs and the private image worker. New components follow Ocharo naming.

## Canonical planning

Use `engineering/planning/backlog.md` and `engineering/planning/version-track/v0.1/v0.1.md`. Previous-work research notes preserve evidence only. Working rules are in `engineering/development/rules.md`; convention verification and open integration gaps are in `engineering/research/convention-audit.md`.

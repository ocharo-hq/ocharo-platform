# Brand and Catalogue adoption

*Last updated: 2026-09-29*

## Ownership and current boundary

Ocharo Platform owns the Brand identity/release companion and Catalogue authoring companion. Their source is now part of this repository with no nested Git root. Existing namespaces, service slugs, IDs, revisions, media hashes and API contracts are preserved. Independent companion solutions/workspaces are a temporary naming exception while full integration is deferred.

The existing authenticated Desk API, PostgreSQL store, worker, organization isolation and contextual catalogue/card editors are unchanged. These companions retain loopback-only write APIs and their own SQLite/media stores. Importing source does not merge their data or authenticate their write endpoints.

Catalogue's Vue routes are operator product editing, collections, copy, stock, pricing and print. Marketing owns the later public Catalogue reader. The existing `/api/public/catalogue` is a sibling integration feed; locale/price status fields and wholesale tiers require a reviewed buyer projection before unrestricted exposure.

## Source provenance

| Donor | Baseline | Adopted source | Documentation |
|---|---|---|---|
| `ventures.ocharo-brand` | Unborn `main`; 101 staged files and two unstaged relocation edits | `engineering/codebase/ocharo-brand.{backend,frontend}-services/` | `product/brand/`; `engineering/{area}/brand/` |
| `ventures.ocharo-catalogue` | `92cf8a13cc1f66458d175c007ffe2f2d92095c5a`; two unstaged relocation edits | `engineering/codebase/ocharo-catalogue.{backend,frontend}-services/` | `product/catalogue/`; `engineering/{area}/catalogue/` |

Donor checkouts, their Git history/indexes and original ignored data are preserved separately by the workspace archival step. Donor CI/agent configuration is not activated in this repository. Root donor READMEs are retained as `source-readme.md` beneath their research folders. The workspace consolidation evidence owns the exact per-file checksums and archival paths.

## Commands

Run backend commands from this repository root. The two companions retain their own package versions and test projects:

```sh
dotnet test engineering/codebase/ocharo-brand.backend-services/OcharoBrand.BackendServices.slnx -p:BuildSpa=false
dotnet test engineering/codebase/ocharo-catalogue.backend-services/OcharoCatalogue.BackendServices.slnx -p:BuildSpa=false
```

Run frontend commands from the selected companion workspace, using its existing lockfile:

```sh
pnpm --dir engineering/codebase/ocharo-brand.frontend-services install --frozen-lockfile
pnpm --dir engineering/codebase/ocharo-brand.frontend-services test
pnpm --dir engineering/codebase/ocharo-brand.frontend-services build
pnpm --dir engineering/codebase/ocharo-catalogue.frontend-services install --frozen-lockfile
pnpm --dir engineering/codebase/ocharo-catalogue.frontend-services test
pnpm --dir engineering/codebase/ocharo-catalogue.frontend-services build
```

Brand API retains 8286 HTTPS / 8287 HTTP; Brand Vite retains 8288. Catalogue API retains 8278 HTTPS / 8279 HTTP; Catalogue Vite retains 8282. Launch only one instance per port. The source import does not start runtimes. The existing Desk API still runs through its compatibility path until its runtime owner retires that link.

## Data and deployment

Existing `App_Data/` files were copied to the corresponding adopted API roots, including SQLite databases and content-addressed media. These directories are ignored by Git and Docker. Keep one active writer per store; archived donors are preservation copies. Product/logo seed inputs remain read-only in the private mft workspace.

Docker/compose reference files live beneath `engineering/deployment/{brand,catalogue}/`. Their relative build contexts point at the common `engineering/codebase/`. They remain separately selected companion descriptors and are not merged into the authenticated Desk deployment. Do not launch both default compose files on their shared example port without an explicit deployment configuration.

## Deferred integration

- Bind Brand/Product records and immutable revisions to the existing organization/brand authorization model.
- Preserve Brand publish/appearance/media disclosure semantics while adopting canonical platform identity.
- Migrate SQLite data to reviewed schemas with stable IDs, media hashes and revision provenance.
- Add an explicitly approved buyer Catalogue projection for Marketing.
- Integrate operator routes, auth, release contracts and cross-app clients; keep safe approved-release fallbacks.
- Consolidate service prefixes, dependency versions and CI only with the full runtime integration.

Verification is recorded in [adoption verification](../previous-work/adoption-verification.md).

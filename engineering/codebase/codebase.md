# Codebase

*Last updated: 2026-10-24*

`brand-workspace.backend-services/BrandWorkspace.BackendServices.slnx` groups the API, worker,
Application, Domain, Infrastructure, Persistence, and meaningful test projects. Projects sit directly
beside the solution; tests sit in `tests/`. Central package versions and per-project lockfiles make
restore inputs explicit. The API serves generated frontend assets from its ignored `wwwroot/`.

`brand-workspace.frontend-services/` contains the Vue application, strict TypeScript, Vite, Tailwind,
public Vue SDK consumption, tests, and a pnpm workspace lockfile. Bootstrap owns preview configuration;
presentation, application, domain, and integration retain their boundaries.

The foundation contains technical diagnostics only. It has no organization membership or content
records. The later preparation milestone adds those through explicit tenant authorization.

Build and runtime commands live in [development](../development/development.md) and
[deployment](../deployment/deployment.md). Evidence belongs in [verification](../research/previous-work/verification.md).

## Adopted companion services

| Domain | Backend | Frontend | Boundary |
|---|---|---|---|
| Brand | `ocharo-brand.backend-services/OcharoBrand.BackendServices.slnx` | `ocharo-brand.frontend-services/` | Identity/release pilot: loopback locally, public when deployed until identity lands; independent SQLite |
| Catalogue | `ocharo-catalogue.backend-services/OcharoCatalogue.BackendServices.slnx` | `ocharo-catalogue.frontend-services/` | Product/price/stock editor: loopback locally, public when deployed until identity lands; independent SQLite |

Each frontend retains its own `apps/web`, lockfile and deploy helper. These preserved service slugs are a temporary naming exception; the foundation prefix remains unchanged. The solutions are not automatically included in the foundation build. [Adoption commands](../research/adoption/adoption.md) select each companion explicitly.

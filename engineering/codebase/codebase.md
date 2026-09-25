# Codebase

*Last updated: 2026-09-26*

`brand-workspace.backend-services/BrandWorkspace.BackendServices.slnx` groups the API, worker,
Application, Domain, Infrastructure, Persistence, and meaningful test projects. Projects sit directly
beside the solution; tests sit in `tests/`. Central package versions and per-project lockfiles make
restore inputs explicit. The API serves generated frontend assets from its ignored `wwwroot/`.

`brand-workspace.frontend-services/` contains the Vue application, strict TypeScript, Vite, Tailwind,
public Vue SDK consumption, tests, and an npm lockfile. Bootstrap owns preview configuration;
presentation, application, domain, and integration retain their boundaries.

The foundation contains technical diagnostics only. It has no organization membership or content
records. The later preparation milestone adds those through explicit tenant authorization.

Build and runtime commands live in [development](../development/development.md) and
[deployment](../deployment/deployment.md). Evidence belongs in [verification](../planning/verification.md).

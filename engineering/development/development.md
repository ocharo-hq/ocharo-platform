# Development

*Last updated: 2026-09-26*

Use shared wow-two conventions; repository documents carry only product-specific choices.
The initial bootstrap is one coordinated lane. Parallel business subsystems need an explicit vector
ownership map before work divides; a frontend/backend split is not a business vector.

The active session may touch this product and the backend/UI SDKs through explicit disjoint lanes.
Studio remains independently owned. Shared package and solution changes have one coordinator.

Backend rules: [backend guidelines](backend-guidelines.md).
Frontend rules: [frontend guidelines](frontend-guidelines.md).
Execution: [scaffold track](../research/previous-work/rough-track/r0.1/r0.1.md).

## Toolchain and verification

The repository `.nvmrc` pins Node `24.11.0`; the frontend pins pnpm through `packageManager`. Run `nvm use` at the
repository root before entering either code directory. The backend `global.json` selects .NET SDK
`10.0.300` with patch roll-forward. The default API build invokes the frontend package manager, so Node belongs on the backend
build process's PATH too.

From `engineering/codebase/brand-workspace.frontend-services/`:

```bash
pnpm install --frozen-lockfile
pnpm lint
pnpm typecheck
pnpm test
```

The root commands delegate to `@brand-workspace/web` in `apps/web/`; shared package extraction stays
inside the frontend workspace. Dependency setup is also exposed by `bash ../../workspace.sh setup`
from the product repository root. That command restores dependencies without starting runtimes.

From `engineering/codebase/brand-workspace.backend-services/`:

```bash
dotnet restore BrandWorkspace.BackendServices.slnx --locked-mode
dotnet build BrandWorkspace.BackendServices.slnx -c Release --no-restore -m:1
dotnet test BrandWorkspace.BackendServices.slnx -c Release --no-build
```

The integration suite requires Docker and owns an isolated PostgreSQL test container. The default
backend build regenerates the frontend with the lockfile; serialize builds touching the API's
`wwwroot`. Use `BuildSpa=false` only when another verified stage has generated those same assets.

Local containers and the explicit migration/probe commands are documented in
[deployment](../deployment/deployment.md). Keep database credentials outside tracked files.

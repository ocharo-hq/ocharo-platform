# Brand Workspace — product-template adoption audit

*Last updated: 2026-09-26*

> Static source audit. Brand Workspace is the working repository name.

Use the existing product-template as an adaptation source after the human's documentation-only first commit. Its current output is not a conformant finished scaffold: Vue, `.slnx`, build integration and several conventions need explicit updates. Preserve the initial repository and import selected adapted files; the shared creation script has no existing-repository import mode.

At the initial audit, no product source, shared script, package, runtime or provider connection was changed. The audit and [source manifest](template-source-manifest.json) were committed in the documentation baseline. Executed adoption evidence is recorded separately below and in [verification](../planning/verification.md).

## Source provenance

The developer's baseline commit is `04a13f5952e15d3ee862647fe0f8757a42046416`.
Before adaptation on 2026-09-26, all 63 selected files matched the recorded SHA-256 values.
Only those files were copied into a task-owned temporary staging directory, excluding generated
output and Git state. Code and deployment configuration are imported additively; the repository's
baseline documents, `.git`, and remote are preserved.

| Source | Observed snapshot | Qualification |
|---|---|---|
| wow-two workspace | `f01df868b1a2c0fe8f1f105fdf7cfc42a4959a28` | Current working tree; shared `scaffold.sh` is modified, with one added and one removed line. Other workspace edits also exist. |
| Product template | `bf6dfacee965e1fafd6cdf84fb0f4167d49f5120` | `git ls-tree -r HEAD` lists only `README.md`. README is modified; `.claude/`, `CLAUDE.md`, `engineering/` and `product/` are untracked. The actual example below comes from this working tree, not committed HEAD. |
| Current scaffold script bytes | SHA-256 `8d5a477947eff71af87f9c9d9b76ea15d85d8fa396c50109df34c3297b711b5b` | Read-only snapshot; retain its existing dirty changes. |

A HEAD pin alone cannot reproduce this template. The [source manifest](template-source-manifest.json) records selected source paths and hashes plus the template HEAD/dirty state. Recheck it immediately before adoption. A `git archive HEAD` would omit the example; blindly copying the entire working tree would include unrelated and generated files.

Relevant owners: [template CLAUDE](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/CLAUDE.md), [create-repo skill](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/.claude/skills/create-repo/SKILL.md:24), and the current conventions cited below. Current conventions supersede the skill's stale claim that rebranding alone produces a conformant React/classic-solution repository.

## Actual template versus required adoption

| Concern | Actual source | Required treatment in the new repository |
|---|---|---|
| Backend | .NET 10; five Clean Architecture projects; nullable and implicit usings enabled; central package management. | Reuse this useful skeleton, then apply current solution grouping, layer ownership and test naming. |
| Solution | `Sample.sln`, a classic solution. Script registers `{Brand}.sln`. | Create `{Brand}.BackendServices.slnx` and register that actual path. Current structure explicitly requires XML `.slnx`. |
| Backend SDK | `WoW2.Sdk.Backend.Beta` pinned to `10.0.21-beta`. | Record this as the template's pin, not the selected current SDK version. Resolve a published, supported pin during implementation and check its actual APIs; do not assume older sample signatures remain current. |
| Other backend pins | OpenAPI `10.0.8`; EF Core/SQLite `10.0.3`; DI/config abstractions `10.0.9`; test SDK `17.12.0`; xUnit `2.9.2`, runner `2.8.2`. | Reconcile transitive floors with the chosen SDK rather than carrying this mixed snapshot automatically. Exact published-version availability was not queried in this audit. |
| Frontend | React/ReactDOM `^19.1.0`, Vite `^6.0.0`, TypeScript `^5.7.2`, React plugin `^4.3.4`. No UI SDK or Tailwind dependency. | Use Vue 3, strict TypeScript, Vite, Tailwind v4 and public exports of `@wow-two-beta/ui-vue`. No React exception is implied by the template. |
| Frontend structure | Small `App.tsx`/`main.tsx` example. No lint/test commands in `package.json`. | Introduce required bootstrap/presentation/application/domain/integration layers and enforce their import boundaries. Add appropriate frontend verification, avoiding empty artificial feature layers. |
| Dependency reproducibility | No frontend lockfile in the inspected source tree; Docker explicitly uses `npm install`. | Pin the package manager and commit a lockfile. Use frozen installs in local and container builds. |
| Host build | API `.csproj` contains references only. Vite emits `dist`; a separate deploy script deletes and replaces API `wwwroot`. Docker copies SPA output before publishing. | Add the required clean-checkout `BuildSpa` path with correct content refresh. Keep generated `wwwroot` out of Git; use `BuildSpa=false` only when a verified container stage supplies SPA output. |
| Development serving | Vite port `8225`; plain HTTP proxy to backend `8221`. | Allocate current ledger ports and configure required HTTPS/mkcert/even-port development plus explicit headless HTTP fallback. |
| API fallback | Global `MapFallbackToFile("index.html")` after controller mapping, without reserved unknown `/api/*` handling. | Add an API JSON-error fallback and test unknown API paths; keep SPA assets public while enforcing API authorization separately. |
| Persistence | SQLite `UseSqlite`; `EnsureCreatedAsync` on startup; greeting-log context. | Treat this as sample storage only. For the planned multi-tenant platform, implement PostgreSQL migrations and tenant constraints deliberately before real persistent business use. |
| Identity/tenancy | Host registers persistence, clock/infrastructure, mediator/application, controllers and OpenAPI. No authentication/authorization or organization isolation wiring is present. | Implement authenticated identity, organization membership and server-derived tenant context; seed Ocharo as an organization. Public signup/billing are separate features. |
| Scheduling/workers | No worker host or durable publication workflow is wired by the sample. Generated package artifacts may include transitive job dependencies. | Package presence is not a configured worker. Build worker composition, durable attempts/outbox, leases and reconciliation as explicit Content & Publishing work. |
| Layering/tests | Query handler is inside Application; one ambiguous `Sample.Tests` project. | Place handlers in Infrastructure under current conventions. Use explicit Unit/Integration/E2E tiers and applicable shared harnesses; create each tier when it has meaningful coverage. |
| Deployment | One API+SPA image and one Compose app using a SQLite volume. Compose publishes fixed `127.0.0.1:8200:8080`. | Adapt to the chosen application/worker/database topology. Allocate the host port explicitly; the script's development port pass does not update this Compose binding. |
| Health | `/health` returns a fixed healthy object. | Preserve a simple liveness endpoint if useful; do not advertise database/queue/provider readiness from this response. Add readiness checks for dependencies actually required by each host. |

Evidence:

- [Backend target](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.backend-services/Directory.Build.props:8), [all backend pins](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.backend-services/Directory.Packages.props:14), [frontend manifest](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.frontend-services/package.json:7).
- [Required Vue standard and scaffold treatment](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/conventions/development/repo/repo-conventions.md:43), [required `.slnx` naming](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/conventions/development/repo/structure/repo-structure.md:125), [frontend layers/boundaries](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/conventions/development/frontend/shapes/app/architecture/architecture.md:12).
- [API project](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.backend-services/Sample.Api/Sample.Api.csproj:1), [deploy copy](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.frontend-services/scripts/deploy.mjs:26), [required host build contract](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/conventions/deployment/hosting/single-host-serving.md:51).
- [Actual Vite proxy](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.frontend-services/vite.config.ts:14), [required HTTPS configuration](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/conventions/development/frontend/shapes/app/platform/dev-server.md:11).
- [Host services](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.backend-services/Sample.Api/Configurations/HostConfigurationExtensions.cs:10), [middleware, health and fallback](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.backend-services/Sample.Api/Configurations/HostConfiguration.cs:19), [API fallback convention](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/conventions/deployment/hosting/single-host-serving.md:46).
- [SQLite and schema creation](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.backend-services/Sample.Persistence/DependencyInjection.cs:13), [Docker build](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/deployment/Dockerfile:14), [Compose host binding](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/deployment/docker-compose.yml:6).
- [Sample query handler](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/workbench/wow-two-sdk-beta/wow-two-sdk-beta.product-template/engineering/codebase/sample.backend-services/Sample.Application/Greetings/Queries/GetGreeting/GetGreetingQueryHandler.cs:6), [required handler ownership](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/conventions/development/backend/dotnet/shapes/service/architecture/clean/clean.md:52), [test tier naming](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/conventions/development/backend/dotnet/shapes/service/architecture/clean/testing.md:38).

## Why the existing script cannot simply be rerun

1. It refuses any existing target directory at [line 69](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/.claude/skills/create-repo/scaffold.sh:69). This is expected after the requested initial repository exists.
2. It copies the current template tree and removes the copied `.git` at [lines 73–77](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/.claude/skills/create-repo/scaffold.sh:73). These operations are for a fresh destination, never for the established venture repository.
3. It derives workspace paths from its own location at [line 30](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/.claude/skills/create-repo/scaffold.sh:30). Copying the unchanged script into `/tmp` would resolve the wrong workspace.
4. It mutates the shared port ledger and active registry; its registration targets classic `.sln`, and an existing short name causes a skip without verifying the recorded path. See [port mutation](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/.claude/skills/create-repo/scaffold.sh:187) and [registration](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/.claude/skills/create-repo/scaffold.sh:233).
5. Its rebrand file globs include `.sln` and `.tsx`, not `.slnx` or `.vue`, at [line 95](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/.claude/skills/create-repo/scaffold.sh:95). Its port rewrite omits Compose at [line 175](/Users/max/Projects/10x-ws/workbench/career/engineering/wow-two/wow-two-ws/.claude/skills/create-repo/scaffold.sh:175).

Do not bypass the refusal, temporarily move the established repository, create a dummy registered project, or change this already-dirty shared script to force the operation. Its useful copy/rebrand recipe can be applied to task-owned staging files without these global side effects.

## Safe sequence after the first commit

1. Complete the documentation-only baseline and human commit handoff. Begin code scaffolding only after the human's initial commit exists. Preserve its SHA and inspect native commit status through the [operations sequence](../operations/operations.md). Automatic commits require the exact user directive and effective ON status; an OFF flag does not prohibit authorized scaffolding or staging.
2. Confirm the working repository identity, read the new repository's instructions, and inspect HEAD/status/index. A final commercial name is not required. Keep all baseline documentation, `.git`, remotes and unrelated edits. Do not replace the repository root.
3. Select template source into a task-owned temporary staging directory. Record provenance and hashes. Exclude `.git`, build output, package caches, local databases, private environment files and generated SPA output. Adapt only this selected copy.
4. Rebrand selected files; produce `.slnx`; implement the required Vue frontend, SDK consumption, locked dependencies, frontend boundaries and single-host build chain. Reconcile current package APIs and versions rather than accepting the sample's old pins implicitly.
5. Import explicit code/config paths into the established repository. Merge needed documentation deliberately with the committed plan. Do not use a blanket sync with deletion or overwrite the baseline instructions.
6. Re-read the port ledger and registration state immediately before scoped edits. Allocate real development and Compose ports once. Update the existing active entry, if present, to the verified `.slnx` and frontend paths; do not add a duplicate.
7. Verify the adapted scaffold with focused frontend and backend checks and a clean publish path. Check unknown API responses, SPA deep links, declared layer boundaries and generated-asset packaging. A passing container build is separate evidence from local host startup or deployment.
8. Commit only the scoped, verified scaffold changes under the user's post-baseline authorization. Record unresolved product capabilities explicitly rather than declaring Content & Publishing implemented by the starter.

## Product foundation remains deliberate work

The repository stays one sibling platform product, with Content & Publishing as its first module. A separate repository does not require a separate microservice per business domain. The first execution worker can share the solution and release source while remaining independently hosted when the durable scheduling increment is implemented.

Keep these acceptance boundaries separate:

- **Conformant scaffold:** correct product/engineering layout; `.slnx`; adapted Vue SDK shell; reproducible dependencies; current layer/test conventions; verified API+SPA build and packaging. This establishes the product foundation, not provider publishing.
- **First persistent platform slice:** real authentication and organization membership; Ocharo organization; tenant-owned records, storage and audit; PostgreSQL migrations; isolation tests across two organizations. No real content/media/connection write should precede this boundary.
- **Durable scheduling slice:** worker composition, durable scheduling state and attempts, transactional outbox, atomic claim/lease behavior, idempotency, edit/cancel races, timezone handling and restart recovery. A timer or imported job package does not satisfy this boundary.
- **Provider publishing slice:** one verified supported channel/content capability; organization-scoped credentials; operator-visible blocked/failed/uncertain states; reconciliation before resending an uncertain external result. A successful mock is distinct from a provider-side publishing proof.

Generic scheduling, integration and automation capabilities belong in the appropriate wow-two libraries; product composition and organization policy belong in the platform repository. Ocharo-specific configuration remains tenant/domain content. Scaffold work need not pre-create empty microservices or every future package.

The existing [Content & Publishing build analysis](/Users/max/Projects/10x-ws/workbench/fam/mft-10x-ws/context/domains/external/work/ventures/ocharo/platform/och-platform-content-publishing-build.md) remains the feature-sequencing reference; this audit corrects assumptions about what the current template supplies.
